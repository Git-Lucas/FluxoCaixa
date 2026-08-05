namespace FluxoCaixa.Consolidado.Api.Persistencia;

internal sealed class LancamentoProcessado
{
    public Guid LancamentoId { get; set; }

    public string ComercianteId { get; set; } = string.Empty;

    public DateTimeOffset ProcessadoEm { get; set; }
}
