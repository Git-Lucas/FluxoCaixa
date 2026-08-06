namespace FluxoCaixa.Lancamentos.Dominio;

public readonly record struct Descricao
{
    private const int _tamanhoMaximo = 200;

    public Descricao(string? valor)
    {
        var normalizada = valor?.Trim() ?? string.Empty;

        if (normalizada.Length == 0)
        {
            throw new LancamentoInvalidoException(
                RegraViolada.DescricaoVazia,
                "A descrição é obrigatória.");
        }

        if (normalizada.Length > _tamanhoMaximo)
        {
            throw new LancamentoInvalidoException(
                RegraViolada.DescricaoMuitoLonga,
                $"A descrição deve ter no máximo {_tamanhoMaximo} caracteres.");
        }

        if (normalizada.Any(char.IsControl))
        {
            throw new LancamentoInvalidoException(
                RegraViolada.DescricaoComCaractereDeControle,
                "A descrição não pode conter caracteres de controle.");
        }

        Valor = normalizada;
    }

    public string Valor { get; }

    public override string ToString() => Valor;
}
