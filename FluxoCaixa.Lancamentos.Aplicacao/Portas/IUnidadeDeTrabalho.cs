namespace FluxoCaixa.Lancamentos.Aplicacao.Portas;

public interface IUnidadeDeTrabalho
{
    Task SalvarAsync(CancellationToken cancellationToken);
}
