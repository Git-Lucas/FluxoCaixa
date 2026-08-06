using FluxoCaixa.Lancamentos.Aplicacao.Portas;
using FluxoCaixa.Lancamentos.Dominio;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;

public sealed class LancamentosDbContext(DbContextOptions<LancamentosDbContext> options, IContextoComerciante contextoComerciante)
    : DbContext(options)
{
    private readonly IContextoComerciante _contextoComerciante = contextoComerciante;

    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();

    internal DbSet<RequisicaoIdempotente> RequisicoesIdempotentes => Set<RequisicaoIdempotente>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LancamentosDbContext).Assembly);

        modelBuilder.AddTransactionalOutboxEntities();

        modelBuilder.Entity<Lancamento>()
            .HasQueryFilter(lancamento => lancamento.ComercianteId == _contextoComerciante.ComercianteId);

        modelBuilder.Entity<RequisicaoIdempotente>()
            .HasQueryFilter(requisicao => requisicao.ComercianteId == _contextoComerciante.ComercianteId);
    }
}
