namespace FluxoCaixa.Plataforma.Autenticacao;

public sealed class OpcoesAutenticacao
{
    public const string SecaoDeConfiguracao = "Autenticacao";

    public required string Authority { get; init; }

    public string Emissor { get; init; } = "fluxocaixa";

    public string Audiencia { get; init; } = "fluxocaixa";

    public string ClaimDoComerciante { get; init; } = "sub";

    public bool RequererHttps { get; init; } = true;
}
