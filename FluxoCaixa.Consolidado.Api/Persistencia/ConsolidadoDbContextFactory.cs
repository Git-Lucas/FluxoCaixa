using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FluxoCaixa.Consolidado.Api.Persistencia;

/// <summary>
/// Permite às ferramentas de design-time do EF Core (`dotnet ef migrations`) construir o
/// <see cref="ConsolidadoDbContext"/> sem o escopo de requisição/consumo que fornece o comerciante
/// em tempo de execução.
/// </summary>
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
