using FluxoCaixa.Consolidado.Api.Consumo;
using Xunit;

namespace FluxoCaixa.Consolidado.Testes.Consumo;

public class ConsumidorDeEventoLancamentoRegistradoTestes
{
    [Fact]
    public void ColunaDoTotal_Credito_RetornaTotalCredito()
        => Assert.Equal("total_credito", ConsumidorDeEventoLancamentoRegistrado.ColunaDoTotal("credito"));

    [Fact]
    public void ColunaDoTotal_Debito_RetornaTotalDebito()
        => Assert.Equal("total_debito", ConsumidorDeEventoLancamentoRegistrado.ColunaDoTotal("debito"));

    [Fact]
    public void ColunaDoTotal_TipoDesconhecido_Lanca()
        => Assert.Throws<InvalidOperationException>(() => ConsumidorDeEventoLancamentoRegistrado.ColunaDoTotal("outro"));
}
