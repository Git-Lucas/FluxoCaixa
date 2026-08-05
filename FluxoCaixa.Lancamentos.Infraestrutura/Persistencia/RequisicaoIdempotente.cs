using FluxoCaixa.Lancamentos.Dominio;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;

internal sealed class RequisicaoIdempotente
{
    public ComercianteId ComercianteId { get; set; }

    public string Chave { get; set; } = string.Empty;

    public string ImpressaoDoConteudo { get; set; } = string.Empty;

    public Guid LancamentoId { get; set; }

    public DateTimeOffset CriadoEm { get; set; }
}
