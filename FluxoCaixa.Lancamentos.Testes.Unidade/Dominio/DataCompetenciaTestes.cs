using FluxoCaixa.Lancamentos.Dominio;
using Xunit;

namespace FluxoCaixa.Lancamentos.Dominio.Testes;

public class DataCompetenciaTestes
{
    private static readonly DateOnly s_dataCorrente = new(2026, 8, 2);

    [Fact]
    public void Criar_CompetenciaFutura_LancaExcecaoDeCompetenciaFutura()
    {
        var competenciaFutura = s_dataCorrente.AddDays(1);

        var excecao = Assert.Throws<LancamentoInvalidoException>(
            () => DataCompetencia.Criar(competenciaFutura, s_dataCorrente));

        Assert.Equal(RegraViolada.CompetenciaFutura, excecao.Regra);
    }

    [Fact]
    public void Criar_CompetenciaIgualADataCorrente_AceitaACompetencia()
    {
        var dataCompetencia = DataCompetencia.Criar(s_dataCorrente, s_dataCorrente);

        Assert.Equal(s_dataCorrente, dataCompetencia.Valor);
    }

    [Fact]
    public void Criar_NoventaDiasAtras_AceitaACompetencia()
    {
        var limiteInferior = s_dataCorrente.AddDays(-90);

        var dataCompetencia = DataCompetencia.Criar(limiteInferior, s_dataCorrente);

        Assert.Equal(limiteInferior, dataCompetencia.Valor);
    }

    [Fact]
    public void Criar_NoventaEUmDiasAtras_LancaExcecaoDeCompetenciaForaDaJanela()
    {
        var alemDoLimite = s_dataCorrente.AddDays(-91);

        var excecao = Assert.Throws<LancamentoInvalidoException>(
            () => DataCompetencia.Criar(alemDoLimite, s_dataCorrente));

        Assert.Equal(RegraViolada.CompetenciaForaDaJanela, excecao.Regra);
    }

    [Fact]
    public void Reconstituir_CompetenciaForaDaJanelaAtual_NaoLancaExcecao()
    {
        var competenciaAntiga = s_dataCorrente.AddDays(-365);

        var dataCompetencia = DataCompetencia.Reconstituir(competenciaAntiga);

        Assert.Equal(competenciaAntiga, dataCompetencia.Valor);
    }
}
