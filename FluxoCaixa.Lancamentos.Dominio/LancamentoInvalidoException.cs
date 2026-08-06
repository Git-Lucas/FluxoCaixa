namespace FluxoCaixa.Lancamentos.Dominio;

public sealed class LancamentoInvalidoException : Exception
{
    public LancamentoInvalidoException(RegraViolada regra, string mensagem)
        : base(mensagem)
    {
        Regra = regra;
    }

    public RegraViolada Regra { get; }
}
