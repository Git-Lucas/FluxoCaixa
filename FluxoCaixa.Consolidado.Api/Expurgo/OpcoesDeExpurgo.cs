namespace FluxoCaixa.Consolidado.Api.Expurgo;

public sealed class OpcoesDeExpurgo
{
    public const string SecaoDeConfiguracao = "ExpurgoDeLancamentoProcessado";

    public TimeSpan Retencao { get; init; } = TimeSpan.FromDays(7);

    public TimeSpan Intervalo { get; init; } = TimeSpan.FromHours(1);
}
