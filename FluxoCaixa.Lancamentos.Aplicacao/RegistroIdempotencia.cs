using FluxoCaixa.Lancamentos.Dominio;

namespace FluxoCaixa.Lancamentos.Aplicacao;

/// <summary>
/// Registro de uma requisição de registro já atendida, usado para detectar reenvio. A chave é
/// opaca e escopada ao comerciante; a impressão do conteúdo permite detectar reenvio com conteúdo
/// divergente do original.
/// </summary>
public sealed record RegistroIdempotencia(
    ComercianteId ComercianteId,
    string Chave,
    Guid LancamentoId,
    string ImpressaoDoConteudo,
    DateTimeOffset CriadoEm);
