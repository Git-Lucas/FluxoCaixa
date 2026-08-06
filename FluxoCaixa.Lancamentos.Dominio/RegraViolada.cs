namespace FluxoCaixa.Lancamentos.Dominio;

public enum RegraViolada
{
    ValorNaoPositivo,
    PrecisaoMonetariaExcedida,
    DescricaoVazia,
    DescricaoMuitoLonga,
    DescricaoComCaractereDeControle,
    TipoDesconhecido,
    CompetenciaFutura,
    CompetenciaForaDaJanela,
}
