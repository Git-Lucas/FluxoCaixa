using FluxoCaixa.Consolidado.Api.Persistencia;

namespace FluxoCaixa.Consolidado.Api.Consulta;

public sealed record ConsolidadoRespostaDto(decimal TotalCredito, decimal TotalDebito, decimal Saldo, DateTimeOffset? AtualizadoEm)
{
    public static ConsolidadoRespostaDto Compor(ConsolidadoDiario? consolidado)
        => consolidado is null
            ? new ConsolidadoRespostaDto(0m, 0m, 0m, null)
            : new ConsolidadoRespostaDto(
                consolidado.TotalCredito,
                consolidado.TotalDebito,
                consolidado.TotalCredito - consolidado.TotalDebito,
                consolidado.AtualizadoEm);
}
