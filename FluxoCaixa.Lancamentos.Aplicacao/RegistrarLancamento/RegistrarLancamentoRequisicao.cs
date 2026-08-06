namespace FluxoCaixa.Lancamentos.Aplicacao.RegistrarLancamento;

public sealed record RegistrarLancamentoRequisicao(
    string ChaveIdempotencia,
    string Tipo,
    decimal Valor,
    DateOnly Competencia,
    string Descricao);
