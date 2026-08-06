namespace FluxoCaixa.Lancamentos.Api.Contratos;

public sealed record RegistrarLancamentoRespostaDto(
    Guid LancamentoId,
    DateTimeOffset RecebidoEm,
    bool Criado);
