namespace FluxoCaixa.Lancamentos.Aplicacao;

/// <summary>
/// Sinaliza que uma chave de idempotência já utilizada foi reapresentada com conteúdo diferente do
/// original.
/// </summary>
public sealed class ConflitoDeConteudoIdempotenteException : Exception;
