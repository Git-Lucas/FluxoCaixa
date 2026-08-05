namespace FluxoCaixa.Identidade.Api.Emissao;

/// <summary>
/// Um comerciante passa a existir quando ganha uma entrada aqui. Relação 1:1 entre cliente e
/// comerciante: o <see cref="ClientId"/> é o identificador do comerciante, a mesma claim <c>sub</c>
/// que os serviços de negócio já leem.
/// </summary>
public sealed class ClienteConfigurado
{
    public required string ClientId { get; init; }

    public required string ClientSecret { get; init; }
}
