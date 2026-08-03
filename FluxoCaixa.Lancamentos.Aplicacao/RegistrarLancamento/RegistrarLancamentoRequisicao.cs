namespace FluxoCaixa.Lancamentos.Aplicacao.RegistrarLancamento;

/// <summary>
/// A chave de idempotência já chega validada em formato (não vazia, no máximo 64 caracteres
/// imprimíveis) — essa validação é feita na borda, antes do caso de uso.
/// </summary>
public sealed record RegistrarLancamentoRequisicao(
    string ChaveIdempotencia,
    string Tipo,
    decimal Valor,
    DateOnly Competencia,
    string Descricao);
