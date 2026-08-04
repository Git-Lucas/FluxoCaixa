using FluxoCaixa.Consolidado.Api.Persistencia;

namespace FluxoCaixa.Consolidado.Api.Consulta;

/// <summary>
/// <see cref="AtualizadoEm"/> é nulo quando a data não teve movimentação: um instante inventado
/// mentiria sobre a frescura da projeção exatamente no caso em que o campo importa (design.md,
/// decisão 6).
/// </summary>
public sealed record ConsolidadoRespostaDto(decimal TotalCredito, decimal TotalDebito, decimal Saldo, DateTimeOffset? AtualizadoEm)
{
    /// <summary>Composição pura, sem acesso a dado: separada para ser testável sem banco.</summary>
    public static ConsolidadoRespostaDto Compor(ConsolidadoDiario? consolidado)
        => consolidado is null
            ? new ConsolidadoRespostaDto(0m, 0m, 0m, null)
            : new ConsolidadoRespostaDto(
                consolidado.TotalCredito,
                consolidado.TotalDebito,
                consolidado.TotalCredito - consolidado.TotalDebito,
                consolidado.AtualizadoEm);
}
