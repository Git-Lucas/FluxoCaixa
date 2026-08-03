using FluxoCaixa.Lancamentos.Aplicacao.Portas;
using FluxoCaixa.Lancamentos.Dominio;
using FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Testes;

/// <summary>
/// Um único container Postgres para toda a coleção, com o outbox transacional do MassTransit
/// registrado sobre transporte em memória — estes testes focam no adaptador Postgres (atomicidade,
/// isolamento, concorrência), não no RabbitMQ, que tem sua própria coleção de testes.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine").Build();
    private ServiceProvider? _servicos;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var colecao = new ServiceCollection();
        colecao.AddDbContext<LancamentosDbContext>(opcoes => opcoes.UseNpgsql(_container.GetConnectionString()));
        colecao.AddScoped<ContextoComerciante>();
        colecao.AddScoped<IContextoComerciante>(provedor => provedor.GetRequiredService<ContextoComerciante>());
        colecao.AddScoped<IDefinidorDeComerciante>(provedor => provedor.GetRequiredService<ContextoComerciante>());
        colecao.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<LancamentosDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
                o.DisableInboxCleanupService();
            });
            x.UsingInMemory();
        });

        _servicos = colecao.BuildServiceProvider();

        await using var escopoDeMigracao = _servicos.CreateAsyncScope();
        var dbContext = escopoDeMigracao.ServiceProvider.GetRequiredService<LancamentosDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_servicos is not null)
        {
            await _servicos.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    /// <summary>Um escopo de DI com o comerciante já definido, para consultas diretas via DbContext.</summary>
    internal EscopoDeTeste CriarEscopo(ComercianteId comercianteId)
    {
        var escopo = _servicos!.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<IDefinidorDeComerciante>().Definir(comercianteId);
        return new EscopoDeTeste(escopo);
    }
}

/// <summary>Agrupa DbContext, repositórios e unidade de trabalho de um único escopo de DI de teste.</summary>
internal sealed class EscopoDeTeste(AsyncServiceScope escopo) : IAsyncDisposable
{
    public LancamentosDbContext DbContext { get; } = escopo.ServiceProvider.GetRequiredService<LancamentosDbContext>();

    public LancamentoRepositorio RepositorioDeLancamento { get; } = new(escopo.ServiceProvider.GetRequiredService<LancamentosDbContext>());

    public RegistroIdempotenciaRepositorio RepositorioDeIdempotencia { get; } = new(escopo.ServiceProvider.GetRequiredService<LancamentosDbContext>());

    public UnidadeDeTrabalhoEfCore UnidadeDeTrabalho { get; } = new(
        escopo.ServiceProvider.GetRequiredService<LancamentosDbContext>(),
        escopo.ServiceProvider.GetRequiredService<IPublishEndpoint>());

    public ValueTask DisposeAsync() => escopo.DisposeAsync();
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
