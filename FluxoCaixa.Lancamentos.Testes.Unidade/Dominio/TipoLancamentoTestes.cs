using FluxoCaixa.Lancamentos.Dominio;
using Xunit;

namespace FluxoCaixa.Lancamentos.Dominio.Testes;

public class TipoLancamentoTestes
{
    [Theory]
    [InlineData("credito", TipoLancamento.Credito)]
    [InlineData("debito", TipoLancamento.Debito)]
    public void Interpretar_ValorConhecido_RetornaOTipoCorrespondente(string valor, TipoLancamento esperado)
    {
        var tipo = TipoLancamentoExtensoes.Interpretar(valor);

        Assert.Equal(esperado, tipo);
    }

    [Theory]
    [InlineData("CREDITO")]
    [InlineData("estorno")]
    [InlineData("")]
    [InlineData(null)]
    public void Interpretar_ValorDesconhecido_LancaExcecaoDeTipoDesconhecido(string? valor)
    {
        var excecao = Assert.Throws<LancamentoInvalidoException>(() => TipoLancamentoExtensoes.Interpretar(valor));

        Assert.Equal(RegraViolada.TipoDesconhecido, excecao.Regra);
    }

    [Fact]
    public void Sinal_Credito_RetornaPositivo()
    {
        Assert.Equal(1, TipoLancamento.Credito.Sinal());
    }

    [Fact]
    public void Sinal_Debito_RetornaNegativo()
    {
        Assert.Equal(-1, TipoLancamento.Debito.Sinal());
    }
}
