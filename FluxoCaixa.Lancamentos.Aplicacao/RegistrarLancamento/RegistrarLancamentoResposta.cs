namespace FluxoCaixa.Lancamentos.Aplicacao.RegistrarLancamento;

public sealed record RegistrarLancamentoResposta(
    Guid LancamentoId,
    DateTimeOffset RecebidoEm,
    bool Criado);
