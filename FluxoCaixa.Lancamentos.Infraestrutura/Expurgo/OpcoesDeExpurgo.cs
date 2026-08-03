namespace FluxoCaixa.Lancamentos.Infraestrutura.Expurgo;

public sealed class OpcoesDeExpurgo
{
    public const string SecaoDeConfiguracao = "ExpurgoDeIdempotencia";

    /// <summary>Validade da garantia de idempotência: 7 dias a contar do registro.</summary>
    public TimeSpan ValidadeDaChave { get; init; } = TimeSpan.FromDays(7);

    public TimeSpan Intervalo { get; init; } = TimeSpan.FromHours(1);
}
