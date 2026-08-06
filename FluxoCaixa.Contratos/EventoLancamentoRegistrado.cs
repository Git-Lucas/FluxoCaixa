namespace FluxoCaixa.Contratos;

public sealed record EventoLancamentoRegistrado(
    Guid LancamentoId,
    string ComercianteId,
    string Tipo,
    decimal Valor,
    DateOnly Competencia,
    DateTimeOffset RecebidoEm);
