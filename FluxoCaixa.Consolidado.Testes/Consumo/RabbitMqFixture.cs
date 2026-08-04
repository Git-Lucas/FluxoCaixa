using System.Collections.Concurrent;
using FluxoCaixa.Consolidado.Api.Consumo;
using FluxoCaixa.Consolidado.Api.Persistencia;
using FluxoCaixa.Consolidado.Testes.Persistencia;
using FluxoCaixa.Contratos;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace FluxoCaixa.Consolidado.Testes.Consumo;

/// <summary>
/// Postgres e RabbitMQ reais: verifica o consumo de ponta a ponta pela fila de verdade, a
/// retentativa com intervalo crescente e a chegada à fila de erro depois de esgotadas as
/// tentativas. Os intervalos de retentativa aqui são muito menores que os de produção (Program.cs)
/// — o mecanismo sob teste é o mesmo, só a escala de tempo muda, para o teste não ficar lento.
/// </summary>
public sealed class RabbitMqFixture : IAsyncLifetime
{
    private const string NomeDaFila = "fluxocaixa.consolidado.testes";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-alpine").Build();
    private IHost? _host;

    public CapturadorDeErros CapturadorDeErros => _host!.Services.GetRequiredService<CapturadorDeErros>();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        _host = ConstrutorDeHostDeTeste.Construir(_postgres.GetConnectionString(), _rabbitMq.GetConnectionString(), NomeDaFila);

        await using (var escopoDeMigracao = _host.Services.CreateAsyncScope())
        {
            var dbContext = escopoDeMigracao.ServiceProvider.GetRequiredService<ConsolidadoDbContext>();
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

    public Task PublicarAsync(EventoLancamentoRegistrado evento)
        => _host!.Services.GetRequiredService<IPublishEndpoint>().Publish(evento);

    internal EscopoDeTeste CriarEscopo(string comercianteId)
    {
        var escopo = _host!.Services.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<IDefinidorDeComerciante>().Definir(comercianteId);
        return new EscopoDeTeste(escopo);
    }
}

[CollectionDefinition(nameof(RabbitMqCollection))]
public sealed class RabbitMqCollection : ICollectionFixture<RabbitMqFixture>;

/// <summary>Captura, em memória, as mensagens que chegam à fila de erro.</summary>
public sealed class CapturadorDeErros
{
    private readonly ConcurrentBag<Guid> _lancamentosSegregados = [];

    public void Adicionar(Guid lancamentoId) => _lancamentosSegregados.Add(lancamentoId);

    public bool Contem(Guid lancamentoId) => _lancamentosSegregados.Contains(lancamentoId);
}

/// <summary>
/// Monta o host mínimo (DbContext + MassTransit) para os testes de consumo via RabbitMQ real, com
/// um segundo endpoint vinculado à fila de erro do primeiro, para capturar o que for segregado.
/// </summary>
internal static class ConstrutorDeHostDeTeste
{
    public static IHost Construir(string connectionStringDoPostgres, string connectionStringDoRabbitMq, string nomeDaFila)
        => Host.CreateDefaultBuilder()
            .ConfigureServices(servicos =>
            {
                servicos.AddDbContext<ConsolidadoDbContext>(opcoes => opcoes.UseNpgsql(connectionStringDoPostgres));
                servicos.AddScoped<ContextoComerciante>();
                servicos.AddScoped<IContextoComerciante>(provedor => provedor.GetRequiredService<ContextoComerciante>());
                servicos.AddScoped<IDefinidorDeComerciante>(provedor => provedor.GetRequiredService<ContextoComerciante>());

                servicos.AddSingleton<CapturadorDeErros>();

                servicos.AddMassTransit(x =>
                {
                    x.AddConsumer<ConsumidorDeEventoLancamentoRegistrado>();

                    x.UsingRabbitMq((contexto, rabbitMq) =>
                    {
                        rabbitMq.Host(new Uri(connectionStringDoRabbitMq));

                        rabbitMq.ReceiveEndpoint(nomeDaFila, endpoint =>
                        {
                            // Mesmo mecanismo do Program.cs (intervalos crescentes, em processo),
                            // com valores pequenos para o teste não esperar minutos.
                            endpoint.UseMessageRetry(retry => retry.Intervals(
                                TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100)));

                            endpoint.ConfigureConsumer<ConsumidorDeEventoLancamentoRegistrado>(contexto);
                        });

                        // Vínculo à fila de erro nativa do MassTransit: sem consumidor bindado nela,
                        // não haveria como observar, do lado do teste, que a segregação aconteceu.
                        // ConfigureConsumeTopology = false é essencial aqui: sem ele, o MassTransit
                        // vincularia esta fila diretamente à exchange do tipo da mensagem (por ela
                        // declarar um Handler<EventoLancamentoRegistrado>), fazendo-a receber uma
                        // cópia de toda publicação — não só o que de fato foi movido para cá após
                        // esgotar as tentativas.
                        rabbitMq.ReceiveEndpoint($"{nomeDaFila}_error", endpoint =>
                        {
                            endpoint.ConfigureConsumeTopology = false;

                            endpoint.Handler<EventoLancamentoRegistrado>(async consumo =>
                            {
                                contexto.GetRequiredService<CapturadorDeErros>().Adicionar(consumo.Message.LancamentoId);
                                await Task.CompletedTask;
                            });
                        });
                    });
                });
            })
            .Build();
}
