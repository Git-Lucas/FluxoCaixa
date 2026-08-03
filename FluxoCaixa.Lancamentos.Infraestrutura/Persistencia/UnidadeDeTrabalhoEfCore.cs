using FluxoCaixa.Lancamentos.Aplicacao;
using FluxoCaixa.Lancamentos.Aplicacao.Portas;
using FluxoCaixa.Lancamentos.Dominio;
using FluxoCaixa.Lancamentos.Infraestrutura.Publicacao;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;

/// <summary>
/// Confirma lançamento, evento de publicação pendente e chave de idempotência numa única
/// transação: uma chamada a <c>SaveChangesAsync</c> sobre um único <see cref="DbContext"/>. O
/// outbox transacional do MassTransit intercepta <see cref="IPublishEndpoint.Publish"/> fora de um
/// consumidor e grava a mensagem nas próprias tabelas de outbox, dentro da mesma transação — o que
/// mantém a camada de aplicação alheia ao mecanismo de publicação.
/// </summary>
internal sealed class UnidadeDeTrabalhoEfCore(LancamentosDbContext dbContext, IPublishEndpoint publishEndpoint) : IUnidadeDeTrabalho
{
    private const string NomeDaRestricaoDeUnicidade = "PK_requisicao_idempotente";

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
        // Materializado antes do laço: publicar pode interagir com o ChangeTracker via
        // interceptor do outbox, o que invalidaria uma enumeração ainda preguiçosa.
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
           && string.Equals(postgresExcecao.ConstraintName, NomeDaRestricaoDeUnicidade, StringComparison.Ordinal);
}
