using FluxoCaixa.Lancamentos.Aplicacao.Portas;
using FluxoCaixa.Lancamentos.Dominio;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;

/// <summary>
/// O isolamento entre comerciantes é imposto aqui, por padrão, via filtro global de consulta.
/// Alcançar dado alheio exige desligar o filtro explicitamente com <c>IgnoreQueryFilters</c>, o que
/// só as tarefas de segundo plano que operam entre comerciantes (expurgo) fazem.
/// </summary>
public sealed class LancamentosDbContext(DbContextOptions<LancamentosDbContext> options, IContextoComerciante contextoComerciante)
    : DbContext(options)
{
    private readonly IContextoComerciante _contextoComerciante = contextoComerciante;

    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();

    internal DbSet<RequisicaoIdempotente> RequisicoesIdempotentes => Set<RequisicaoIdempotente>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LancamentosDbContext).Assembly);

        // Tabelas do outbox transacional do MassTransit (InboxState, OutboxState, OutboxMessage):
        // substituem a tabela de outbox própria que existia antes da migração para o MassTransit.
        modelBuilder.AddTransactionalOutboxEntities();

        modelBuilder.Entity<Lancamento>()
            .HasQueryFilter(lancamento => lancamento.ComercianteId == _contextoComerciante.ComercianteId);

        modelBuilder.Entity<RequisicaoIdempotente>()
            .HasQueryFilter(requisicao => requisicao.ComercianteId == _contextoComerciante.ComercianteId);
    }
}
