using FluxoCaixa.Lancamentos.Dominio;

namespace FluxoCaixa.Lancamentos.Aplicacao;

public sealed record RegistroIdempotencia(
    ComercianteId ComercianteId,
    string Chave,
    Guid LancamentoId,
    string ImpressaoDoConteudo,
    DateTimeOffset CriadoEm);
