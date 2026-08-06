using FluxoCaixa.Contratos;
using FluxoCaixa.Lancamentos.Aplicacao;
using FluxoCaixa.Lancamentos.Aplicacao.Portas;
using FluxoCaixa.Lancamentos.Dominio;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;

internal sealed class UnidadeDeTrabalhoEfCore(LancamentosDbContext dbContext, IPublishEndpoint publishEndpoint) : IUnidadeDeTrabalho
{
    private const string _nomeDaRestricaoDeUnicidade = "PK_requisicao_idempotente";

    private readonly LancamentosDbContext _dbContext = dbContext;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    public async Task SalvarAsync(CancellationToken cancellationToken)
    {
        await PublicarEventosParaLancamentosNovosAsync(cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException excecao) when (EhViolacaoDeUnicidadeDeIdempotencia(excecao))
        {
            throw new ConflitoDeIdempotenciaException();
        }
    }

    private async Task PublicarEventosParaLancamentosNovosAsync(CancellationToken cancellationToken)
    {
        var lancamentosNovos = _dbContext.ChangeTracker.Entries<Lancamento>()
            .Where(entrada => entrada.State == EntityState.Added)
            .Select(entrada => entrada.Entity)
            .ToList();

        foreach (var lancamento in lancamentosNovos)
        {
            var evento = new EventoLancamentoRegistrado(
                lancamento.Id,
                lancamento.ComercianteId.Valor,
                lancamento.Tipo.ParaContrato(),
                lancamento.Valor.Valor,
                lancamento.Competencia.Valor,
                lancamento.RecebidoEm);

            await _publishEndpoint.Publish(evento, cancellationToken);
        }
    }

    private static bool EhViolacaoDeUnicidadeDeIdempotencia(DbUpdateException excecao)
        => excecao.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresExcecao
           && string.Equals(postgresExcecao.ConstraintName, _nomeDaRestricaoDeUnicidade, StringComparison.Ordinal);
}
