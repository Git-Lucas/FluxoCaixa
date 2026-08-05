using FluxoCaixa.Lancamentos.Dominio;
using Xunit;

namespace FluxoCaixa.Lancamentos.Dominio.Testes;

public class LancamentoTestes
{
    private static readonly DateOnly _dataCorrente = new(2026, 8, 2);

    [Fact]
    public void Registrar_DoisLancamentosEmSequencia_GeraIdentificadorMaiorParaOSegundo()
    {
        var primeiro = CriarLancamento();

        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        SpinWait.SpinUntil(() => cronometro.ElapsedMilliseconds >= 2);

        var segundo = CriarLancamento();

        Assert.True(segundo.Id.CompareTo(primeiro.Id) > 0);
    }

    [Fact]
    public void Registrar_CompetenciaRetroativa_PreservaInstanteDeRecebimentoDistinto()
    {
        var recebidoEm = new DateTimeOffset(2026, 8, 2, 10, 0, 0, TimeSpan.Zero);
        var competenciaRetroativa = _dataCorrente.AddDays(-90);

        var lancamento = Lancamento.Registrar(
            "comerciante-1",
            "credito",
            100m,
            competenciaRetroativa,
            _dataCorrente,
            "venda de balcão",
            recebidoEm);

        Assert.Equal(competenciaRetroativa, lancamento.Competencia.Valor);
        Assert.Equal(recebidoEm, lancamento.RecebidoEm);
        Assert.NotEqual(lancamento.Competencia.Valor, DateOnly.FromDateTime(lancamento.RecebidoEm.DateTime));
    }

    private static Lancamento CriarLancamento()
        => Lancamento.Registrar(
            "comerciante-1",
            "credito",
            100m,
            _dataCorrente,
            _dataCorrente,
            "venda de balcão",
            DateTimeOffset.UtcNow);
}
