namespace FluxoCaixa.Consolidado.Api.Expurgo;

public sealed class OpcoesDeExpurgo
{
    public const string SecaoDeConfiguracao = "ExpurgoDeLancamentoProcessado";

    /// <summary>Retenção do registro de deduplicação: 7 dias a contar do processamento.</summary>
    public TimeSpan Retencao { get; init; } = TimeSpan.FromDays(7);

    public TimeSpan Intervalo { get; init; } = TimeSpan.FromHours(1);
}
