using System.Diagnostics.Metrics;

namespace FluxoCaixa.Consolidado.Api.Consumo;

internal static class MetricasDoConsolidado
{
    private static readonly Meter _meter = new("FluxoCaixa.Consolidado");

    public static readonly Histogram<double> DefasagemDeConsolidacaoSegundos = _meter.CreateHistogram<double>(
        "fluxocaixa.consolidado.defasagem_segundos",
        unit: "s",
        description: "Tempo entre o registro do lançamento e o seu reflexo no consolidado.");

    public static readonly Counter<long> MensagensSegregadas = _meter.CreateCounter<long>(
        "fluxocaixa.consolidado.mensagens_segregadas",
        unit: "{mensagem}",
        description: "Mensagens movidas para a fila de erro por falha persistente de processamento.");
}
