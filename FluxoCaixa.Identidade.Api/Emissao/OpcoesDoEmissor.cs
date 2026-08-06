namespace FluxoCaixa.Identidade.Api.Emissao;

public sealed class OpcoesDoEmissor
{
    public const string SecaoDeConfiguracao = "Emissao";

    public required IReadOnlyList<ClienteConfigurado> Clientes { get; init; }

    public string Emissor { get; init; } = "fluxocaixa";

    public string Audiencia { get; init; } = "fluxocaixa";

    public TimeSpan Validade { get; init; } = TimeSpan.FromMinutes(15);
}
