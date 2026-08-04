namespace FluxoCaixa.Consolidado.Api.Persistencia;

/// <summary>
/// Registro de deduplicação: um lançamento já refletido em <see cref="ConsolidadoDiario"/>. Chave
/// primária pelo identificador do lançamento — é ela que garante a exclusão sob entregas
/// concorrentes do mesmo evento.
/// </summary>
internal sealed class LancamentoProcessado
{
    public Guid LancamentoId { get; set; }

    public string ComercianteId { get; set; } = string.Empty;

    public DateTimeOffset ProcessadoEm { get; set; }
}
