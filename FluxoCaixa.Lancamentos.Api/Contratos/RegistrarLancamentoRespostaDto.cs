namespace FluxoCaixa.Lancamentos.Api.Contratos;

/// <summary>
/// <paramref name="Criado"/> distingue criação (status 201) de reenvio (status 200); o campo existe
/// além do status para que o cliente não precise depender só do código HTTP.
/// </summary>
public sealed record RegistrarLancamentoRespostaDto(
    Guid LancamentoId,
    DateTimeOffset RecebidoEm,
    bool Criado);
