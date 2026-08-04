using FluxoCaixa.Consolidado.Api.Consulta;
using FluxoCaixa.Consolidado.Api.Persistencia;
using Xunit;

namespace FluxoCaixa.Consolidado.Testes.Consulta;

public class ConsolidadoRespostaDtoTestes
{
    [Fact]
    public void Compor_SemLinha_RetornaTotaisESaldoZeradosEAtualizadoEmNulo()
    {
        var resposta = ConsolidadoRespostaDto.Compor(null);

        Assert.Equal(0m, resposta.TotalCredito);
        Assert.Equal(0m, resposta.TotalDebito);
        Assert.Equal(0m, resposta.Saldo);
        Assert.Null(resposta.AtualizadoEm);
    }

    [Fact]
    public void Compor_ComLinha_SaldoEhADiferencaEntreCreditoEDebito()
    {
        var atualizadoEm = new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
        var consolidado = new ConsolidadoDiario
        {
            ComercianteId = "comerciante-1",
            Competencia = new DateOnly(2026, 8, 2),
            TotalCredito = 300m,
            TotalDebito = 120m,
            AtualizadoEm = atualizadoEm,
        };

        var resposta = ConsolidadoRespostaDto.Compor(consolidado);

        Assert.Equal(300m, resposta.TotalCredito);
        Assert.Equal(120m, resposta.TotalDebito);
        Assert.Equal(180m, resposta.Saldo);
        Assert.Equal(atualizadoEm, resposta.AtualizadoEm);
    }

    [Fact]
    public void Compor_DebitoMaiorQueCredito_SaldoEhNegativo()
    {
        var consolidado = new ConsolidadoDiario
        {
            ComercianteId = "comerciante-1",
            Competencia = new DateOnly(2026, 8, 2),
            TotalCredito = 50m,
            TotalDebito = 200m,
            AtualizadoEm = DateTimeOffset.UtcNow,
        };

        var resposta = ConsolidadoRespostaDto.Compor(consolidado);

        Assert.Equal(-150m, resposta.Saldo);
    }
}
