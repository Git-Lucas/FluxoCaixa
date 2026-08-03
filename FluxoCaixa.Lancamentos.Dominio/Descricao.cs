namespace FluxoCaixa.Lancamentos.Dominio;

/// <summary>
/// Descrição do lançamento: obrigatória, não vazia após remoção de espaços nas extremidades,
/// limitada a 200 caracteres e livre de caracteres de controle.
/// </summary>
public readonly record struct Descricao
{
    private const int TamanhoMaximo = 200;

    public Descricao(string? valor)
    {
        var normalizada = valor?.Trim() ?? string.Empty;

        if (normalizada.Length == 0)
        {
            throw new LancamentoInvalidoException(
                RegraViolada.DescricaoVazia,
                "A descrição é obrigatória.");
        }

        if (normalizada.Length > TamanhoMaximo)
        {
            throw new LancamentoInvalidoException(
                RegraViolada.DescricaoMuitoLonga,
                $"A descrição deve ter no máximo {TamanhoMaximo} caracteres.");
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
