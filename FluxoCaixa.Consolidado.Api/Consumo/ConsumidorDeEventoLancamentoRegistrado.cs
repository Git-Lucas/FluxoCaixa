using FluxoCaixa.Consolidado.Api.Persistencia;
using FluxoCaixa.Contratos;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Consolidado.Api.Consumo;

internal sealed class ConsumidorDeEventoLancamentoRegistrado(
    ConsolidadoDbContext dbContext,
    IContextoComerciante contextoComerciante,
    IDefinidorDeComerciante definidorDeComerciante) : IConsumer<EventoLancamentoRegistrado>
{
    public async Task Consume(ConsumeContext<EventoLancamentoRegistrado> context)
    {
        var evento = context.Message;
        definidorDeComerciante.Definir(evento.ComercianteId);
        var comercianteId = contextoComerciante.ComercianteId;
        var agora = DateTimeOffset.UtcNow;

        await using var transacao = await dbContext.Database.BeginTransactionAsync(context.CancellationToken);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({evento.LancamentoId.ToString()}))",
            context.CancellationToken);

        var linhasInseridas = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO lancamento_processado (lancamento_id, comerciante_id, processado_em)
            VALUES ({evento.LancamentoId}, {comercianteId}, {agora})
            ON CONFLICT (lancamento_id) DO NOTHING
            """,
            context.CancellationToken);

        if (linhasInseridas == 0)
        {
            await transacao.CommitAsync(context.CancellationToken);
            return;
        }

        var colunaDoTotal = ColunaDoTotal(evento.Tipo);

        var totalCredito = evento.Tipo == "credito" ? evento.Valor : 0m;
        var totalDebito = evento.Tipo == "debito" ? evento.Valor : 0m;

        var sql =
            "INSERT INTO consolidado_diario (comerciante_id, competencia, total_credito, total_debito, atualizado_em) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}) " +
            "ON CONFLICT (comerciante_id, competencia) DO UPDATE SET " +
            colunaDoTotal + " = consolidado_diario." + colunaDoTotal + " + excluded." + colunaDoTotal + ", " +
            "atualizado_em = excluded.atualizado_em";

#pragma warning disable S2077
        await dbContext.Database.ExecuteSqlRawAsync(
            sql,
            [comercianteId, evento.Competencia, totalCredito, totalDebito, agora],
            context.CancellationToken);
#pragma warning restore S2077

        await transacao.CommitAsync(context.CancellationToken);

        MetricasDoConsolidado.DefasagemDeConsolidacaoSegundos.Record((agora - evento.RecebidoEm).TotalSeconds);
    }

    internal static string ColunaDoTotal(string tipo) => tipo switch
    {
        "credito" => "total_credito",
        "debito" => "total_debito",
        _ => throw new InvalidOperationException($"Tipo de lançamento desconhecido: '{tipo}'."),
    };
}
