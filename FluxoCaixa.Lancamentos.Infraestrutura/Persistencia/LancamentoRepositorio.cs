using FluxoCaixa.Lancamentos.Aplicacao.Portas;
using FluxoCaixa.Lancamentos.Dominio;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;

internal sealed class LancamentoRepositorio(LancamentosDbContext dbContext) : ILancamentoRepositorio
{
    private readonly LancamentosDbContext _dbContext = dbContext;

    public void Adicionar(Lancamento lancamento) => _dbContext.Lancamentos.Add(lancamento);

    public Task<Lancamento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
        => _dbContext.Lancamentos.SingleOrDefaultAsync(lancamento => lancamento.Id == id, cancellationToken);
}
