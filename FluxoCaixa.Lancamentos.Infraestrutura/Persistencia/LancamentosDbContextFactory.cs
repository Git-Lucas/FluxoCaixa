using FluxoCaixa.Lancamentos.Aplicacao.Portas;
using FluxoCaixa.Lancamentos.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;

public sealed class LancamentosDbContextFactory : IDesignTimeDbContextFactory<LancamentosDbContext>
{
    public LancamentosDbContext CreateDbContext(string[] args)
    {
        string stringDeConexaoDeDesignTime = string.Empty;

        var opcoes = new DbContextOptionsBuilder<LancamentosDbContext>()
            .UseNpgsql(stringDeConexaoDeDesignTime)
            .Options;

        return new LancamentosDbContext(opcoes, new ContextoComercianteIndisponivelEmTempoDeDesign());
    }

    private sealed class ContextoComercianteIndisponivelEmTempoDeDesign : IContextoComerciante
    {
        public ComercianteId ComercianteId
            => throw new NotSupportedException("O contexto de comerciante não está disponível em tempo de design.");
    }
}
