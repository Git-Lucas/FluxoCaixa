using FluxoCaixa.Lancamentos.Dominio;
using FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Testes;

[Collection(nameof(PostgresCollection))]
public class ExpurgoDeIdempotenciaTestes(PostgresFixture fixture)
{
    private static readonly DateOnly s_dataCorrente = new(2026, 8, 2);

    private readonly PostgresFixture _fixture = fixture;

    [Fact]
    public async Task Expurgar_ChaveComMaisDeSeteDias_EhRemovidaEChaveRecenteEhPreservada()
    {
        var comerciante = new ComercianteId($"comerciante-{Guid.NewGuid()}");

        var lancamento = Lancamento.Registrar(
            comerciante.Valor,
            "credito",
            100m,
            s_dataCorrente,
            s_dataCorrente,
            "venda de balcão",
            DateTimeOffset.UtcNow);

        await using (var escopo = _fixture.CriarEscopo(comerciante))
        {
            escopo.RepositorioDeLancamento.Adicionar(lancamento);
            escopo.DbContext.Add(new RequisicaoIdempotente
            {
                ComercianteId = comerciante,
                Chave = "chave-expirada",
                ImpressaoDoConteudo = "impressao-1",
                LancamentoId = lancamento.Id,
                CriadoEm = DateTimeOffset.UtcNow.AddDays(-8),
            });
            escopo.DbContext.Add(new RequisicaoIdempotente
            {
                ComercianteId = comerciante,
                Chave = "chave-recente",
                ImpressaoDoConteudo = "impressao-2",
                LancamentoId = lancamento.Id,
                CriadoEm = DateTimeOffset.UtcNow.AddDays(-1),
            });

            await escopo.DbContext.SaveChangesAsync();
        }

        var limite = DateTimeOffset.UtcNow - TimeSpan.FromDays(7);
        await using (var escopoDeExpurgo = _fixture.CriarEscopo(comerciante))
        {
            await escopoDeExpurgo.DbContext.RequisicoesIdempotentes
                .IgnoreQueryFilters()
                .Where(requisicao => requisicao.CriadoEm < limite)
                .ExecuteDeleteAsync();
        }

        await using var escopoDeLeitura = _fixture.CriarEscopo(comerciante);
        var chavesRestantes = await escopoDeLeitura.DbContext.RequisicoesIdempotentes
            .Select(requisicao => requisicao.Chave)
            .ToListAsync();

        Assert.DoesNotContain("chave-expirada", chavesRestantes);
        Assert.Contains("chave-recente", chavesRestantes);
    }
}
