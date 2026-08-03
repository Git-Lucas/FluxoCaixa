using FluxoCaixa.Lancamentos.Dominio;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;

/// <summary>
/// Registro de persistência de uma requisição de registro já atendida. Chave primária composta de
/// (comerciante, chave) — o mecanismo de exclusão sob concorrência da idempotência.
/// </summary>
internal sealed class RequisicaoIdempotente
{
    public ComercianteId ComercianteId { get; set; }

    public string Chave { get; set; } = string.Empty;

    public string ImpressaoDoConteudo { get; set; } = string.Empty;

    public Guid LancamentoId { get; set; }

    public DateTimeOffset CriadoEm { get; set; }
}
