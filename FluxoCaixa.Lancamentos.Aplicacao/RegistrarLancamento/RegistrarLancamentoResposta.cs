namespace FluxoCaixa.Lancamentos.Aplicacao.RegistrarLancamento;

/// <summary>
/// <paramref name="Criado"/> distingue um lançamento recém-criado (<c>true</c>) de um reenvio de
/// requisição já atendida (<c>false</c>).
/// </summary>
public sealed record RegistrarLancamentoResposta(
    Guid LancamentoId,
    DateTimeOffset RecebidoEm,
    bool Criado);
