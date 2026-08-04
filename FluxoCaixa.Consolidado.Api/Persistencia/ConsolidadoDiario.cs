namespace FluxoCaixa.Consolidado.Api.Persistencia;

/// <summary>
/// Projeção pré-agregada do saldo de um comerciante numa data. Uma linha por (comerciante,
/// competência); a consulta é um acesso indexado direto a ela, sem agregação sob demanda.
/// </summary>
public sealed class ConsolidadoDiario
{
    public string ComercianteId { get; set; } = string.Empty;

    public DateOnly Competencia { get; set; }

    public decimal TotalCredito { get; set; }

    public decimal TotalDebito { get; set; }

    public DateTimeOffset AtualizadoEm { get; set; }
}
