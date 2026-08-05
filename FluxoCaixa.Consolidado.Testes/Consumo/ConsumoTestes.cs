using FluxoCaixa.Consolidado.Api.Persistencia;
using FluxoCaixa.Consolidado.Testes.Infraestrutura;
using FluxoCaixa.Consolidado.Testes.Persistencia;
using FluxoCaixa.Contratos;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FluxoCaixa.Consolidado.Testes.Consumo;

[Collection(nameof(PostgresCollection))]
public class ConsumoTestes(PostgresFixture fixture)
{
    private static readonly DateOnly _competencia = new(2026, 8, 2);

    private readonly PostgresFixture _fixture = fixture;

    [Fact]
    public async Task Consumir_TransacaoFalhaEntreAsDuasEscritas_NaoMarcaNemSomaNada()
    {
        var comerciante = NovoComerciante();
        var evento = new EventoLancamentoRegistrado(Guid.NewGuid(), comerciante, "tipo-desconhecido", 100m, _competencia, DateTimeOffset.UtcNow);

        await _fixture.PublicarAsync(evento);

        await Task.Delay(1000);

        await using var escopo = _fixture.CriarEscopo(comerciante);
        Assert.False(await escopo.DbContext.ConsolidadosDiarios.AnyAsync());
        Assert.False(await escopo.DbContext.LancamentosProcessados.AnyAsync(l => l.LancamentoId == evento.LancamentoId));
    }

    [Fact]
    public async Task Consumir_MesmoLancamentoEntregueDuasVezesConcorrentemente_SomaUmaUnicaVez()
    {
        var comerciante = NovoComerciante();
        var evento = new EventoLancamentoRegistrado(Guid.NewGuid(), comerciante, "credito", 150m, _competencia, DateTimeOffset.UtcNow);

        await Task.WhenAll(_fixture.PublicarAsync(evento), _fixture.PublicarAsync(evento));

        var consolidado = await AguardarConsolidadoAsync(comerciante, _competencia);

        Assert.NotNull(consolidado);
        Assert.Equal(150m, consolidado.TotalCredito);

        await using var escopo = _fixture.CriarEscopo(comerciante);
        Assert.Equal(1, await escopo.DbContext.LancamentosProcessados.CountAsync(l => l.LancamentoId == evento.LancamentoId));
    }

    [Fact]
    public async Task Consumir_LancamentosDoMesmoDiaEmParalelo_NaoPerdeAtualizacao()
    {
        var comerciante = NovoComerciante();
        var valores = Enumerable.Range(1, 20).Select(indice => (decimal)indice).ToArray();
        var eventos = valores
            .Select(valor => new EventoLancamentoRegistrado(Guid.NewGuid(), comerciante, "credito", valor, _competencia, DateTimeOffset.UtcNow))
            .ToArray();

        await Task.WhenAll(eventos.Select(evento => _fixture.PublicarAsync(evento)));

        var totalEsperado = valores.Sum();
        var consolidado = await AguardarConsolidadoComTotalAsync(comerciante, _competencia, totalEsperado);

        Assert.NotNull(consolidado);
        Assert.Equal(totalEsperado, consolidado.TotalCredito);
    }

    [Fact]
    public async Task Consumir_CompetenciaRetroativa_AfetaApenasAquelaData()
    {
        var comerciante = NovoComerciante();
        var competenciaRetroativa = _competencia.AddDays(-90);

        var eventoHoje = new EventoLancamentoRegistrado(Guid.NewGuid(), comerciante, "credito", 200m, _competencia, DateTimeOffset.UtcNow);
        var eventoRetroativo = new EventoLancamentoRegistrado(Guid.NewGuid(), comerciante, "credito", 50m, competenciaRetroativa, DateTimeOffset.UtcNow);

        await _fixture.PublicarAsync(eventoHoje);
        await _fixture.PublicarAsync(eventoRetroativo);

        var consolidadoDeHoje = await AguardarConsolidadoAsync(comerciante, _competencia);
        var consolidadoRetroativo = await AguardarConsolidadoAsync(comerciante, competenciaRetroativa);

        Assert.NotNull(consolidadoDeHoje);
        Assert.Equal(200m, consolidadoDeHoje.TotalCredito);
        Assert.NotNull(consolidadoRetroativo);
        Assert.Equal(50m, consolidadoRetroativo.TotalCredito);
    }

    [Fact]
    public async Task Consumir_ComerciantesDistintos_NaoMisturaOsConsolidados()
    {
        var comercianteA = NovoComerciante();
        var comercianteB = NovoComerciante();

        await _fixture.PublicarAsync(new EventoLancamentoRegistrado(Guid.NewGuid(), comercianteA, "credito", 300m, _competencia, DateTimeOffset.UtcNow));
        await _fixture.PublicarAsync(new EventoLancamentoRegistrado(Guid.NewGuid(), comercianteB, "credito", 999m, _competencia, DateTimeOffset.UtcNow));

        var consolidadoDeA = await AguardarConsolidadoAsync(comercianteA, _competencia);
        var consolidadoDeB = await AguardarConsolidadoAsync(comercianteB, _competencia);

        Assert.NotNull(consolidadoDeA);
        Assert.Equal(300m, consolidadoDeA.TotalCredito);
        Assert.NotNull(consolidadoDeB);
        Assert.Equal(999m, consolidadoDeB.TotalCredito);

        await using var escopoDeA = _fixture.CriarEscopo(comercianteA);
        var consolidadosVisiveisParaA = await escopoDeA.DbContext.ConsolidadosDiarios.ToListAsync();
        Assert.Single(consolidadosVisiveisParaA);
        Assert.Equal(comercianteA, consolidadosVisiveisParaA[0].ComercianteId);
    }

    private static string NovoComerciante() => $"comerciante-{Guid.NewGuid()}";

    private Task<ConsolidadoDiario?> AguardarConsolidadoAsync(string comercianteId, DateOnly competencia)
        => Aguardar.AteAsync(async () =>
        {
            await using var escopo = _fixture.CriarEscopo(comercianteId);
            return await escopo.DbContext.ConsolidadosDiarios
                .SingleOrDefaultAsync(consolidado => consolidado.Competencia == competencia);
        });

    private Task<ConsolidadoDiario?> AguardarConsolidadoComTotalAsync(string comercianteId, DateOnly competencia, decimal totalCreditoEsperado)
        => Aguardar.AteAsync(async () =>
        {
            await using var escopo = _fixture.CriarEscopo(comercianteId);
            var consolidado = await escopo.DbContext.ConsolidadosDiarios
                .SingleOrDefaultAsync(consolidado => consolidado.Competencia == competencia);
            return consolidado is not null && consolidado.TotalCredito == totalCreditoEsperado ? consolidado : null;
        });
}
