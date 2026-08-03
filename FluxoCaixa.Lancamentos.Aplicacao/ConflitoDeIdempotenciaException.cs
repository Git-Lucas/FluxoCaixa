namespace FluxoCaixa.Lancamentos.Aplicacao;

/// <summary>
/// Sinaliza que a chave de idempotência já foi gravada por uma requisição concorrente, detectado
/// pela violação do índice único de (comerciante, chave) no momento da confirmação.
/// </summary>
public sealed class ConflitoDeIdempotenciaException : Exception;
