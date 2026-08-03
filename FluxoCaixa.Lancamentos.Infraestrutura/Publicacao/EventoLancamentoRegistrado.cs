namespace FluxoCaixa.Lancamentos.Infraestrutura.Publicacao;

/// <summary>
/// Contrato do evento publicado para o serviço de consolidado. O identificador é estável entre
/// republicações, para deduplicação pelo consumidor. A descrição não é publicada.
/// </summary>
public sealed record EventoLancamentoRegistrado(
    Guid LancamentoId,
    string ComercianteId,
    string Tipo,
    decimal Valor,
    DateOnly Competencia,
    DateTimeOffset RecebidoEm);
