namespace FluxoCaixa.Identidade.Api.Emissao;

public sealed class ClienteConfigurado
{
    public required string ClientId { get; init; }

    public required string ClientSecret { get; init; }
}
