using FluxoCaixa.Lancamentos.Aplicacao;
using FluxoCaixa.Lancamentos.Aplicacao.Portas;
using FluxoCaixa.Lancamentos.Dominio;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;

internal sealed class RegistroIdempotenciaRepositorio(LancamentosDbContext dbContext) : IRegistroIdempotencia
{
    private readonly LancamentosDbContext _dbContext = dbContext;

    public async Task<RegistroIdempotencia?> ObterAsync(ComercianteId comercianteId, string chave, CancellationToken cancellationToken)
    {
        var requisicao = await _dbContext.RequisicoesIdempotentes
            .SingleOrDefaultAsync(
                requisicao => requisicao.ComercianteId == comercianteId && requisicao.Chave == chave,
                cancellationToken);

        return requisicao is null
            ? null
            : new RegistroIdempotencia(
                requisicao.ComercianteId,
                requisicao.Chave,
                requisicao.LancamentoId,
                requisicao.ImpressaoDoConteudo,
                requisicao.CriadoEm);
    }

    public void Adicionar(RegistroIdempotencia registro)
        => _dbContext.RequisicoesIdempotentes.Add(new RequisicaoIdempotente
        {
            ComercianteId = registro.ComercianteId,
            Chave = registro.Chave,
            ImpressaoDoConteudo = registro.ImpressaoDoConteudo,
            LancamentoId = registro.LancamentoId,
            CriadoEm = registro.CriadoEm,
        });
}
