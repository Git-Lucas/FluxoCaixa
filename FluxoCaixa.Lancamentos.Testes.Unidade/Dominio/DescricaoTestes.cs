using FluxoCaixa.Lancamentos.Dominio;
using Xunit;

namespace FluxoCaixa.Lancamentos.Dominio.Testes;

public class DescricaoTestes
{
    [Fact]
    public void Construtor_DescricaoNula_LancaExcecaoDeDescricaoVazia()
    {
        var excecao = Assert.Throws<LancamentoInvalidoException>(() => new Descricao(null));

        Assert.Equal(RegraViolada.DescricaoVazia, excecao.Regra);
    }

    [Fact]
    public void Construtor_ApenasEspacos_LancaExcecaoDeDescricaoVazia()
    {
        var excecao = Assert.Throws<LancamentoInvalidoException>(() => new Descricao("   "));

        Assert.Equal(RegraViolada.DescricaoVazia, excecao.Regra);
    }

    [Fact]
    public void Construtor_DuzentosCaracteres_AceitaADescricao()
    {
        var valor = new string('a', 200);

        var descricao = new Descricao(valor);

        Assert.Equal(valor, descricao.Valor);
    }

    [Fact]
    public void Construtor_DuzentosEUmCaracteres_LancaExcecaoDeDescricaoMuitoLonga()
    {
        var valor = new string('a', 201);

        var excecao = Assert.Throws<LancamentoInvalidoException>(() => new Descricao(valor));

        Assert.Equal(RegraViolada.DescricaoMuitoLonga, excecao.Regra);
    }

    [Fact]
    public void Construtor_CaractereDeControle_LancaExcecaoDeCaractereDeControle()
    {
        var excecao = Assert.Throws<LancamentoInvalidoException>(() => new Descricao("vendabalcão"));

        Assert.Equal(RegraViolada.DescricaoComCaractereDeControle, excecao.Regra);
    }

    [Fact]
    public void Construtor_EspacosNasExtremidades_RemoveOsEspacos()
    {
        var descricao = new Descricao("  venda de balcão  ");

        Assert.Equal("venda de balcão", descricao.Valor);
    }
}
