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
    private static readonly DateOnly s_competencia = new(2026, 8, 2);

    private readonly PostgresFixture _fixture = fixture;

    [Fact]
    public async Task Consumir_TransacaoFalhaEntreAsDuasEscritas_NaoMarcaNemSomaNada()
    {
        var comerciante = NovoComerciante();
        var evento = new EventoLancamentoRegistrado(Guid.NewGuid(), comerciante, "tipo-desconhecido", 100m, s_competencia, DateTimeOffset.UtcNow);

        // "tipo-desconhecido" faz o consumidor lançar depois de marcar a deduplicação mas antes de
        // somar o agregado, ainda dentro da mesma transação — o cenário exato de
        // "marcação e soma são atômicas".
        await _fixture.PublicarAsync(evento);

        // Não há resultado positivo a esperar aqui (é uma ausência); aguarda o tempo que a mensagem
        // levaria para ser processada e falhar antes de checar.
        await Task.Delay(1000);

        await using var escopo = _fixture.CriarEscopo(comerciante);
        Assert.False(await escopo.DbContext.ConsolidadosDiarios.AnyAsync());
        Assert.False(await escopo.DbContext.LancamentosProcessados.AnyAsync(l => l.LancamentoId == evento.LancamentoId));
    }

    [Fact]
    public async Task Consumir_MesmoLancamentoEntregueDuasVezesConcorrentemente_SomaUmaUnicaVez()
    {
        var comerciante = NovoComerciante();
        var evento = new EventoLancamentoRegistrado(Guid.NewGuid(), comerciante, "credito", 150m, s_competencia, DateTimeOffset.UtcNow);

        await Task.WhenAll(_fixture.PublicarAsync(evento), _fixture.PublicarAsync(evento));

        var consolidado = await AguardarConsolidadoAsync(comerciante, s_competencia);

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
            .Select(valor => new EventoLancamentoRegistrado(Guid.NewGuid(), comerciante, "credito", valor, s_competencia, DateTimeOffset.UtcNow))
            .ToArray();

        await Task.WhenAll(eventos.Select(evento => _fixture.PublicarAsync(evento)));

        var totalEsperado = valores.Sum();
        var consolidado = await AguardarConsolidadoComTotalAsync(comerciante, s_competencia, totalEsperado);

        Assert.NotNull(consolidado);
        Assert.Equal(totalEsperado, consolidado.TotalCredito);
    }

    [Fact]
    public async Task Consumir_CompetenciaRetroativa_AfetaApenasAquelaData()
    {
        var comerciante = NovoComerciante();
        var competenciaRetroativa = s_competencia.AddDays(-90);

        var eventoHoje = new EventoLancamentoRegistrado(Guid.NewGuid(), comerciante, "credito", 200m, s_competencia, DateTimeOffset.UtcNow);
        var eventoRetroativo = new EventoLancamentoRegistrado(Guid.NewGuid(), comerciante, "credito", 50m, competenciaRetroativa, DateTimeOffset.UtcNow);

        await _fixture.PublicarAsync(eventoHoje);
        await _fixture.PublicarAsync(eventoRetroativo);

        var consolidadoDeHoje = await AguardarConsolidadoAsync(comerciante, s_competencia);
        var consolidadoRetroativo = await AguardarConsolidadoAsync(comerciante, competenciaRetroativa);

        Assert.NotNull(consolidadoDeHoje);
        Assert.Equal(200m, consolidadoDeHoje.TotalCredito);
        Assert.NotNull(consolidadoRetroativo);
        Assert.Equal(50m, consolidadoRetroativo.TotalCredito);
    }

    [Fact]
    public async Task Consumir_EntregaEmOrdemInvertida_ProduzOMesmoSaldo()
    {
        var comercianteOrdemDireta = NovoComerciante();
        var comercianteOrdemInvertida = NovoComerciante();

        var credito = 100m;
        var debito = 30m;

        var creditoDireto = new EventoLancamentoRegistrado(Guid.NewGuid(), comercianteOrdemDireta, "credito", credito, s_competencia, DateTimeOffset.UtcNow);
        var debitoDireto = new EventoLancamentoRegistrado(Guid.NewGuid(), comercianteOrdemDireta, "debito", debito, s_competencia, DateTimeOffset.UtcNow);
        await _fixture.PublicarAsync(creditoDireto);
        await _fixture.PublicarAsync(debitoDireto);

        var creditoInvertido = new EventoLancamentoRegistrado(Guid.NewGuid(), comercianteOrdemInvertida, "credito", credito, s_competencia, DateTimeOffset.UtcNow);
        var debitoInvertido = new EventoLancamentoRegistrado(Guid.NewGuid(), comercianteOrdemInvertida, "debito", debito, s_competencia, DateTimeOffset.UtcNow);
        await _fixture.PublicarAsync(debitoInvertido);
        await _fixture.PublicarAsync(creditoInvertido);

        var consolidadoOrdemDireta = await AguardarConsolidadoAsync(comercianteOrdemDireta, s_competencia);
        var consolidadoOrdemInvertida = await AguardarConsolidadoAsync(comercianteOrdemInvertida, s_competencia);

        Assert.NotNull(consolidadoOrdemDireta);
        Assert.NotNull(consolidadoOrdemInvertida);
        Assert.Equal(consolidadoOrdemDireta.TotalCredito - consolidadoOrdemDireta.TotalDebito, consolidadoOrdemInvertida.TotalCredito - consolidadoOrdemInvertida.TotalDebito);
    }

    [Fact]
    public async Task Consumir_ComerciantesDistintos_NaoMisturaOsConsolidados()
    {
        var comercianteA = NovoComerciante();
        var comercianteB = NovoComerciante();

        await _fixture.PublicarAsync(new EventoLancamentoRegistrado(Guid.NewGuid(), comercianteA, "credito", 300m, s_competencia, DateTimeOffset.UtcNow));
        await _fixture.PublicarAsync(new EventoLancamentoRegistrado(Guid.NewGuid(), comercianteB, "credito", 999m, s_competencia, DateTimeOffset.UtcNow));

        var consolidadoDeA = await AguardarConsolidadoAsync(comercianteA, s_competencia);
        var consolidadoDeB = await AguardarConsolidadoAsync(comercianteB, s_competencia);

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
