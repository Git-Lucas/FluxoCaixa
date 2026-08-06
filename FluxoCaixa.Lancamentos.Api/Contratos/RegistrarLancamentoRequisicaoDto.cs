namespace FluxoCaixa.Lancamentos.Api.Contratos;

public sealed record RegistrarLancamentoRequisicaoDto(
    string Tipo,
    decimal Valor,
    DateOnly Competencia,
    string Descricao);
