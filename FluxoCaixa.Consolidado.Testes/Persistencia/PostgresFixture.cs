using FluxoCaixa.Consolidado.Api.Consumo;
using FluxoCaixa.Consolidado.Api.Persistencia;
using FluxoCaixa.Contratos;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace FluxoCaixa.Consolidado.Testes.Persistencia;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine").Build();
    private ServiceProvider? _servicos;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var colecao = new ServiceCollection();
        colecao.AddDbContext<ConsolidadoDbContext>(opcoes => opcoes.UseNpgsql(_container.GetConnectionString()));
        colecao.AddScoped<ContextoComerciante>();
        colecao.AddScoped<IContextoComerciante>(provedor => provedor.GetRequiredService<ContextoComerciante>());
        colecao.AddScoped<IDefinidorDeComerciante>(provedor => provedor.GetRequiredService<ContextoComerciante>());

        colecao.AddMassTransit(x =>
        {
            x.AddConsumer<ConsumidorDeEventoLancamentoRegistrado>();
            x.UsingInMemory((contexto, cfg) => cfg.ConfigureEndpoints(contexto));
        });

        _servicos = colecao.BuildServiceProvider();

        await using (var escopoDeMigracao = _servicos.CreateAsyncScope())
        {
            var dbContext = escopoDeMigracao.ServiceProvider.GetRequiredService<ConsolidadoDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        await _servicos.GetRequiredService<IBusControl>().StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_servicos is not null)
        {
            await _servicos.GetRequiredService<IBusControl>().StopAsync();
            await _servicos.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    public Task PublicarAsync(EventoLancamentoRegistrado evento)
        => _servicos!.GetRequiredService<IPublishEndpoint>().Publish(evento);

    internal EscopoDeTeste CriarEscopo(string comercianteId)
    {
        var escopo = _servicos!.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<IDefinidorDeComerciante>().Definir(comercianteId);
        return new EscopoDeTeste(escopo);
    }
}

internal sealed class EscopoDeTeste(AsyncServiceScope escopo) : IAsyncDisposable
{
    public ConsolidadoDbContext DbContext { get; } = escopo.ServiceProvider.GetRequiredService<ConsolidadoDbContext>();

    public ValueTask DisposeAsync() => escopo.DisposeAsync();
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
