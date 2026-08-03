namespace FluxoCaixa.Lancamentos.Api.Contratos;

/// <summary>
/// Nenhum campo de comerciante existe neste contrato — o comerciante vem exclusivamente da
/// credencial, nunca do corpo da requisição.
/// </summary>
public sealed record RegistrarLancamentoRequisicaoDto(
    string Tipo,
    decimal Valor,
    DateOnly Competencia,
    string Descricao);
