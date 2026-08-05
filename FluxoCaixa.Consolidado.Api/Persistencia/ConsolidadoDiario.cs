namespace FluxoCaixa.Consolidado.Api.Persistencia;

public sealed class ConsolidadoDiario
{
    public string ComercianteId { get; set; } = string.Empty;

    public DateOnly Competencia { get; set; }

    public decimal TotalCredito { get; set; }

    public decimal TotalDebito { get; set; }

    public DateTimeOffset AtualizadoEm { get; set; }
}
