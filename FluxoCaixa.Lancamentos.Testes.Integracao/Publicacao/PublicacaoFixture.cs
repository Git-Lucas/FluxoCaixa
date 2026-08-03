using System.Collections.Concurrent;
using FluxoCaixa.Lancamentos.Aplicacao.Portas;
using FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;
using FluxoCaixa.Lancamentos.Infraestrutura.Publicacao;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Testes;

/// <summary>
/// Postgres e RabbitMQ reais juntos: publicar via MassTransit passa pelo outbox transacional (EF
/// Core sobre Postgres) antes de chegar ao broker, então testar a entrega de fato exige os dois.
///
/// Lançamentos não declara fila própria para o evento publicado — ver o comentário em
/// <c>InfraestruturaServiceCollectionExtensions</c>. O host de teste também registra um handler
/// para <see cref="EventoLancamentoRegistrado"/>, simulando o futuro consumidor (o serviço de
/// consolidado), para que exista uma fila vinculada ao exchange do evento antes de qualquer
/// publicação — do contrário, a mensagem seria descartada por falta de fila (comportamento normal
/// de exchange fanout sem vínculo).
/// </summary>
public sealed class PublicacaoFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-alpine").Build();
    private IHost? _host;

    public IServiceProvider Servicos => _host!.Services;

    public CapturadorDeEventos Capturador => _host!.Services.GetRequiredService<CapturadorDeEventos>();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        _host = ConstrutorDeHostDeTeste.Construir(_postgres.GetConnectionString(), _rabbitMq.GetConnectionString(), "fluxocaixa.lancamentos.registrados.testes");

        await using (var escopoDeMigracao = _host.Services.CreateAsyncScope())
        {
            var dbContext = escopoDeMigracao.ServiceProvider.GetRequiredService<LancamentosDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        await _postgres.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }
}

/// <summary>Captura, em memória, os eventos recebidos pelo consumidor de teste.</summary>
public sealed class CapturadorDeEventos
{
    private readonly ConcurrentBag<EventoLancamentoRegistrado> _eventos = [];

    public void Adicionar(EventoLancamentoRegistrado evento) => _eventos.Add(evento);

    public EventoLancamentoRegistrado? Encontrar(Guid lancamentoId) => _eventos.FirstOrDefault(e => e.LancamentoId == lancamentoId);
}

/// <summary>
/// Monta o host mínimo (DbContext + MassTransit) compartilhado pelos testes de publicação, com um
/// handler de teste representando o futuro consumidor do evento.
/// </summary>
internal static class ConstrutorDeHostDeTeste
{
    public static IHost Construir(string connectionStringDoPostgres, string connectionStringDoRabbitMq, string nomeDaFila)
        => Host.CreateDefaultBuilder()
            .ConfigureServices(servicos =>
            {
                servicos.AddDbContext<LancamentosDbContext>(opcoes => opcoes.UseNpgsql(connectionStringDoPostgres));
                servicos.AddScoped<ContextoComerciante>();
                servicos.AddScoped<IContextoComerciante>(provedor => provedor.GetRequiredService<ContextoComerciante>());
                servicos.AddScoped<IDefinidorDeComerciante>(provedor => provedor.GetRequiredService<ContextoComerciante>());

                servicos.AddSingleton<CapturadorDeEventos>();

                servicos.AddMassTransit(x =>
                {
                    x.AddEntityFrameworkOutbox<LancamentosDbContext>(o =>
                    {
                        o.UsePostgres();
                        o.UseBusOutbox();
                        o.DisableInboxCleanupService();
                    });

                    x.UsingRabbitMq((contexto, rabbitMq) =>
                    {
                        rabbitMq.Host(new Uri(connectionStringDoRabbitMq));

                        // Representa o futuro consumidor (o consolidado): sem isso, a mensagem
                        // publicada não teria fila vinculada e seria descartada pelo broker.
                        rabbitMq.ReceiveEndpoint(nomeDaFila, endpoint =>
                        {
                            endpoint.Handler<EventoLancamentoRegistrado>(async consumo =>
                            {
                                contexto.GetRequiredService<CapturadorDeEventos>().Adicionar(consumo.Message);
                                await Task.CompletedTask;
                            });
                        });
                    });
                });
            })
            .Build();
}

[CollectionDefinition(nameof(PublicacaoCollection))]
public sealed class PublicacaoCollection : ICollectionFixture<PublicacaoFixture>;
