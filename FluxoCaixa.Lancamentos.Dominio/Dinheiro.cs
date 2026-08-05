using System.Globalization;

namespace FluxoCaixa.Lancamentos.Dominio;

public readonly record struct Dinheiro
{
    public Dinheiro(decimal valor)
    {
        if (valor <= 0)
        {
            throw new LancamentoInvalidoException(
                RegraViolada.ValorNaoPositivo,
                "O valor deve ser estritamente positivo.");
        }

        if (decimal.Round(valor, 2) != valor)
        {
            throw new LancamentoInvalidoException(
                RegraViolada.PrecisaoMonetariaExcedida,
                "O valor não pode ter mais de duas casas decimais.");
        }

        Valor = valor;
    }

    public decimal Valor { get; }

    public override string ToString() => Valor.ToString("F2", CultureInfo.InvariantCulture);
}
