using FluxoCaixa.Lancamentos.Dominio;

namespace FluxoCaixa.Lancamentos.Aplicacao.Portas;

public interface ILancamentoRepositorio
{
    void Adicionar(Lancamento lancamento);

    Task<Lancamento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
}
