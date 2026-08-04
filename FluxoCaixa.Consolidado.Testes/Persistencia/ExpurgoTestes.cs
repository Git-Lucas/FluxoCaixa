using FluxoCaixa.Consolidado.Api.Persistencia;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FluxoCaixa.Consolidado.Testes.Persistencia;

[Collection(nameof(PostgresCollection))]
public class ExpurgoTestes(PostgresFixture fixture)
{
    private readonly PostgresFixture _fixture = fixture;

    [Fact]
    public async Task Expurgar_RegistroComMaisDeSeteDias_EhRemovidoERegistroRecenteEhPreservado()
    {
        var comerciante = $"comerciante-{Guid.NewGuid()}";

        await using (var escopo = _fixture.CriarEscopo(comerciante))
        {
            escopo.DbContext.Add(new LancamentoProcessado
            {
                LancamentoId = Guid.NewGuid(),
                ComercianteId = comerciante,
                ProcessadoEm = DateTimeOffset.UtcNow.AddDays(-8),
            });
            escopo.DbContext.Add(new LancamentoProcessado
            {
                LancamentoId = Guid.NewGuid(),
                ComercianteId = comerciante,
                ProcessadoEm = DateTimeOffset.UtcNow.AddDays(-1),
            });

            await escopo.DbContext.SaveChangesAsync();
        }

        var limite = DateTimeOffset.UtcNow - TimeSpan.FromDays(7);
        await using (var escopoDeExpurgo = _fixture.CriarEscopo(comerciante))
        {
            await escopoDeExpurgo.DbContext.LancamentosProcessados
                .IgnoreQueryFilters()
                .Where(lancamento => lancamento.ProcessadoEm < limite)
                .ExecuteDeleteAsync();
        }

        await using var escopoDeLeitura = _fixture.CriarEscopo(comerciante);
        var restantes = await escopoDeLeitura.DbContext.LancamentosProcessados
            .Select(lancamento => lancamento.ProcessadoEm)
            .ToListAsync();

        Assert.Single(restantes);
        Assert.True(restantes[0] > limite);
    }
}
