namespace FluxoCaixa.Lancamentos.Dominio;

/// <summary>
/// Tipo do lançamento. Determina o efeito sobre o saldo: crédito aumenta, débito diminui.
/// </summary>
public enum TipoLancamento
{
    Credito,
    Debito,
}

public static class TipoLancamentoExtensoes
{
    private const string ContratoCredito = "credito";
    private const string ContratoDebito = "debito";

    /// <summary>
    /// Interpreta o tipo a partir do valor recebido no contrato da interface (`credito` ou
    /// `debito`). Qualquer outro valor viola a regra de domínio.
    /// </summary>
    public static TipoLancamento Interpretar(string? valor) => valor switch
    {
        ContratoCredito => TipoLancamento.Credito,
        ContratoDebito => TipoLancamento.Debito,
        _ => throw new LancamentoInvalidoException(
            RegraViolada.TipoDesconhecido,
            $"O tipo do lançamento deve ser '{ContratoCredito}' ou '{ContratoDebito}'."),
    };

    public static string ParaContrato(this TipoLancamento tipo) => tipo switch
    {
        TipoLancamento.Credito => ContratoCredito,
        TipoLancamento.Debito => ContratoDebito,
        _ => throw new LancamentoInvalidoException(
            RegraViolada.TipoDesconhecido,
            $"O tipo do lançamento deve ser '{ContratoCredito}' ou '{ContratoDebito}'."),
    };

    /// <summary>
    /// Efeito do lançamento sobre o saldo: +1 para crédito, -1 para débito. O sinal do valor
    /// monetário em si nunca carrega esse efeito.
    /// </summary>
    public static int Sinal(this TipoLancamento tipo) => tipo switch
    {
        TipoLancamento.Credito => 1,
        TipoLancamento.Debito => -1,
        _ => throw new LancamentoInvalidoException(
            RegraViolada.TipoDesconhecido,
            $"O tipo do lançamento deve ser '{ContratoCredito}' ou '{ContratoDebito}'."),
    };
}
