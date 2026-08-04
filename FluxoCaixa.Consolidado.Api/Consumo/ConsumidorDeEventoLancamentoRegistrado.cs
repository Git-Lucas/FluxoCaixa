using FluxoCaixa.Consolidado.Api.Persistencia;
using FluxoCaixa.Contratos;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Consolidado.Api.Consumo;

/// <summary>
/// Consome o evento de lançamento e mantém a projeção diária atualizada. O comerciante da operação
/// vem da mensagem, não de credencial — não há uma aqui —, mas alimenta o mesmo
/// <see cref="IContextoComerciante"/> usado pela consulta, para que a autoridade sobre "de quem é
/// este dado" permaneça única (design.md, decisão 5).
/// </summary>
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

        // Deduplicação antes de qualquer efeito sobre o agregado: 0 linhas afetadas significa que
        // este lançamento já foi consolidado — nunca detectado por exceção de violação de
        // unicidade, que abortaria a transação inteira (design.md, decisão 4).
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

        // Incremento atômico via SQL bruto, não leitura-modificação-escrita via EF Core: dois
        // consumidores processando lançamentos do mesmo dia em paralelo não podem perder atualização
        // (design.md, decisão 4). O ON CONFLICT mira só a linha (comerciante, competência) do
        // próprio evento — nenhuma outra data é alcançada.
        //
        // colunaDoTotal entra por concatenação, não por parâmetro — nome de coluna não é
        // parametrizável em SQL — mas só pode valer "total_credito" ou "total_debito", fixados no
        // switch acima; os valores de fato (comercianteId, totais, datas) são todos parametrizados.
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

        // Commit antes do retorno: o MassTransit confirma a mensagem somente depois que o método
        // termina sem lançar exceção, então o commit precisa acontecer aqui dentro (design.md,
        // decisão 4 — "ack só depois do commit").
        await transacao.CommitAsync(context.CancellationToken);
    }

    /// <summary>
    /// Nome da coluna restrito às duas únicas saídas deste switch — nunca derivado de valor externo
    /// não validado — porque não pode ser passado como parâmetro de SQL. Pura e separada para ser
    /// testável sem banco.
    /// </summary>
    internal static string ColunaDoTotal(string tipo) => tipo switch
    {
        "credito" => "total_credito",
        "debito" => "total_debito",
        _ => throw new InvalidOperationException($"Tipo de lançamento desconhecido: '{tipo}'."),
    };
}
