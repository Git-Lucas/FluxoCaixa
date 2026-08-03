using FluxoCaixa.Lancamentos.Dominio;
using Xunit;

namespace FluxoCaixa.Lancamentos.Dominio.Testes;

public class LancamentoTestes
{
    private static readonly DateOnly s_dataCorrente = new(2026, 8, 2);

    [Fact]
    public void Registrar_DoisLancamentosEmSequencia_GeraIdentificadorMaiorParaOSegundo()
    {
        var primeiro = CriarLancamento();

        // UUID v7 é ordenável por prefixo de tempo com resolução de milissegundo: a garantia só é
        // observável entre milissegundos distintos, não entre chamadas consecutivas na mesma janela.
        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        SpinWait.SpinUntil(() => cronometro.ElapsedMilliseconds >= 2);

        var segundo = CriarLancamento();

        Assert.True(segundo.Id.CompareTo(primeiro.Id) > 0);
    }

    [Fact]
    public void Registrar_CompetenciaRetroativa_PreservaInstanteDeRecebimentoDistinto()
    {
        var recebidoEm = new DateTimeOffset(2026, 8, 2, 10, 0, 0, TimeSpan.Zero);
        var competenciaRetroativa = s_dataCorrente.AddDays(-90);

        var lancamento = Lancamento.Registrar(
            "comerciante-1",
            "credito",
            100m,
            competenciaRetroativa,
            s_dataCorrente,
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
            s_dataCorrente,
            s_dataCorrente,
            "venda de balcão",
            DateTimeOffset.UtcNow);
}
