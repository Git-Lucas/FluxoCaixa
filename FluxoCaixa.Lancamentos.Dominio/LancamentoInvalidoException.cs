namespace FluxoCaixa.Lancamentos.Dominio;

/// <summary>
/// Sinaliza violação de uma invariante de negócio do lançamento. A mensagem descreve a regra
/// violada em linguagem de negócio e nunca contém detalhe interno de implementação.
/// </summary>
public sealed class LancamentoInvalidoException : Exception
{
    public LancamentoInvalidoException(RegraViolada regra, string mensagem)
        : base(mensagem)
    {
        Regra = regra;
    }

    public RegraViolada Regra { get; }
}
