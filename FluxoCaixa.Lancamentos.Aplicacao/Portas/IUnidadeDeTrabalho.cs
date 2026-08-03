namespace FluxoCaixa.Lancamentos.Aplicacao.Portas;

public interface IUnidadeDeTrabalho
{
    /// <summary>
    /// Confirma o lançamento, a mensagem de publicação pendente e o registro de idempotência na
    /// mesma transação. Lança <see cref="ConflitoDeIdempotenciaException"/> quando a chave de
    /// idempotência já foi gravada por uma requisição concorrente.
    /// </summary>
    Task SalvarAsync(CancellationToken cancellationToken);
}
