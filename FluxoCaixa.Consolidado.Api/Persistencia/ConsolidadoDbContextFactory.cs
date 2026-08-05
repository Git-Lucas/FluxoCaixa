using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FluxoCaixa.Consolidado.Api.Persistencia;

public sealed class ConsolidadoDbContextFactory : IDesignTimeDbContextFactory<ConsolidadoDbContext>
{
    public ConsolidadoDbContext CreateDbContext(string[] args)
    {
        string stringDeConexaoDeDesignTime = string.Empty;

        var opcoes = new DbContextOptionsBuilder<ConsolidadoDbContext>()
            .UseNpgsql(stringDeConexaoDeDesignTime)
            .Options;

        return new ConsolidadoDbContext(opcoes, new ContextoComercianteIndisponivelEmTempoDeDesign());
    }

    private sealed class ContextoComercianteIndisponivelEmTempoDeDesign : IContextoComerciante
    {
        public string ComercianteId
            => throw new NotSupportedException("O contexto de comerciante não está disponível em tempo de design.");
    }
}
