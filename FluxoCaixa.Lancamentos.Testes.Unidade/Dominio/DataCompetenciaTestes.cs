using FluxoCaixa.Lancamentos.Dominio;
using Xunit;

namespace FluxoCaixa.Lancamentos.Dominio.Testes;

public class DataCompetenciaTestes
{
    private static readonly DateOnly _dataCorrente = new(2026, 8, 2);

    [Fact]
    public void Criar_CompetenciaFutura_LancaExcecaoDeCompetenciaFutura()
    {
        var competenciaFutura = _dataCorrente.AddDays(1);

        var excecao = Assert.Throws<LancamentoInvalidoException>(
            () => DataCompetencia.Criar(competenciaFutura, _dataCorrente));

        Assert.Equal(RegraViolada.CompetenciaFutura, excecao.Regra);
    }

    [Fact]
    public void Criar_CompetenciaIgualADataCorrente_AceitaACompetencia()
    {
        var dataCompetencia = DataCompetencia.Criar(_dataCorrente, _dataCorrente);

        Assert.Equal(_dataCorrente, dataCompetencia.Valor);
    }

    [Fact]
    public void Criar_NoventaDiasAtras_AceitaACompetencia()
    {
        var limiteInferior = _dataCorrente.AddDays(-90);

        var dataCompetencia = DataCompetencia.Criar(limiteInferior, _dataCorrente);

        Assert.Equal(limiteInferior, dataCompetencia.Valor);
    }

    [Fact]
    public void Criar_NoventaEUmDiasAtras_LancaExcecaoDeCompetenciaForaDaJanela()
    {
        var alemDoLimite = _dataCorrente.AddDays(-91);

        var excecao = Assert.Throws<LancamentoInvalidoException>(
            () => DataCompetencia.Criar(alemDoLimite, _dataCorrente));

        Assert.Equal(RegraViolada.CompetenciaForaDaJanela, excecao.Regra);
    }

    [Fact]
    public void Reconstituir_CompetenciaForaDaJanelaAtual_NaoLancaExcecao()
    {
        var competenciaAntiga = _dataCorrente.AddDays(-365);

        var dataCompetencia = DataCompetencia.Reconstituir(competenciaAntiga);

        Assert.Equal(competenciaAntiga, dataCompetencia.Valor);
    }
}
