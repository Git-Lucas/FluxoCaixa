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

        var (totalCredito, totalDebito) = TotaisDoLancamento(evento.Tipo, evento.Valor);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO consolidado_diario (comerciante_id, competencia, total_credito, total_debito, atualizado_em)
            VALUES ({comercianteId}, {evento.Competencia}, {totalCredito}, {totalDebito}, {agora})
            ON CONFLICT (comerciante_id, competencia) DO UPDATE SET
                total_credito = consolidado_diario.total_credito + excluded.total_credito,
                total_debito = consolidado_diario.total_debito + excluded.total_debito,
                atualizado_em = excluded.atualizado_em
            """,
            context.CancellationToken);

        await transacao.CommitAsync(context.CancellationToken);

        MetricasDoConsolidado.DefasagemDeConsolidacaoSegundos.Record((agora - evento.RecebidoEm).TotalSeconds);
    }

    internal static (decimal TotalCredito, decimal TotalDebito) TotaisDoLancamento(string tipo, decimal valor) => tipo switch
    {
        "credito" => (valor, 0m),
        "debito" => (0m, valor),
        _ => throw new InvalidOperationException($"Tipo de lançamento desconhecido: '{tipo}'."),
    };
}
