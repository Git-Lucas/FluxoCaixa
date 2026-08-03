using FluxoCaixa.Lancamentos.Dominio;
using Xunit;

namespace FluxoCaixa.Lancamentos.Dominio.Testes;

public class DinheiroTestes
{
    [Fact]
    public void Construtor_ValorZero_LancaExcecaoDeValorNaoPositivo()
    {
        var excecao = Assert.Throws<LancamentoInvalidoException>(() => new Dinheiro(0m));

        Assert.Equal(RegraViolada.ValorNaoPositivo, excecao.Regra);
    }

    [Fact]
    public void Construtor_ValorNegativo_LancaExcecaoDeValorNaoPositivo()
    {
        var excecao = Assert.Throws<LancamentoInvalidoException>(() => new Dinheiro(-10m));

        Assert.Equal(RegraViolada.ValorNaoPositivo, excecao.Regra);
    }

    [Fact]
    public void Construtor_TresCasasDecimais_LancaExcecaoDePrecisaoExcedida()
    {
        var excecao = Assert.Throws<LancamentoInvalidoException>(() => new Dinheiro(10.001m));

        Assert.Equal(RegraViolada.PrecisaoMonetariaExcedida, excecao.Regra);
    }

    [Fact]
    public void Construtor_DuasCasasDecimais_AceitaOValor()
    {
        var dinheiro = new Dinheiro(10.55m);

        Assert.Equal(10.55m, dinheiro.Valor);
    }

    [Fact]
    public void Construtor_ValorInteiro_AceitaOValor()
    {
        var dinheiro = new Dinheiro(10m);

        Assert.Equal(10m, dinheiro.Valor);
    }
}
