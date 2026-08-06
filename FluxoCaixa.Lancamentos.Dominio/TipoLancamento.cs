namespace FluxoCaixa.Lancamentos.Dominio;

public enum TipoLancamento
{
    Credito,
    Debito,
}

public static class TipoLancamentoExtensoes
{
    private const string _contratoCredito = "credito";
    private const string _contratoDebito = "debito";

    public static TipoLancamento Interpretar(string? valor) => valor switch
    {
        _contratoCredito => TipoLancamento.Credito,
        _contratoDebito => TipoLancamento.Debito,
        _ => throw new LancamentoInvalidoException(
            RegraViolada.TipoDesconhecido,
            $"O tipo do lançamento deve ser '{_contratoCredito}' ou '{_contratoDebito}'."),
    };

    public static string ParaContrato(this TipoLancamento tipo) => tipo switch
    {
        TipoLancamento.Credito => _contratoCredito,
        TipoLancamento.Debito => _contratoDebito,
        _ => throw new LancamentoInvalidoException(
            RegraViolada.TipoDesconhecido,
            $"O tipo do lançamento deve ser '{_contratoCredito}' ou '{_contratoDebito}'."),
    };

    public static int Sinal(this TipoLancamento tipo) => tipo switch
    {
        TipoLancamento.Credito => 1,
        TipoLancamento.Debito => -1,
        _ => throw new LancamentoInvalidoException(
            RegraViolada.TipoDesconhecido,
            $"O tipo do lançamento deve ser '{_contratoCredito}' ou '{_contratoDebito}'."),
    };
}
