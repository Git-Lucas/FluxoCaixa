using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Consolidado.Api.Persistencia;

public sealed class ConsolidadoDbContext(DbContextOptions<ConsolidadoDbContext> options, IContextoComerciante contextoComerciante)
    : DbContext(options)
{
    private readonly IContextoComerciante _contextoComerciante = contextoComerciante;

    public DbSet<ConsolidadoDiario> ConsolidadosDiarios => Set<ConsolidadoDiario>();

    internal DbSet<LancamentoProcessado> LancamentosProcessados => Set<LancamentoProcessado>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConsolidadoDbContext).Assembly);

        modelBuilder.Entity<ConsolidadoDiario>()
            .HasQueryFilter(consolidado => consolidado.ComercianteId == _contextoComerciante.ComercianteId);

        modelBuilder.Entity<LancamentoProcessado>()
            .HasQueryFilter(lancamento => lancamento.ComercianteId == _contextoComerciante.ComercianteId);
    }
}
