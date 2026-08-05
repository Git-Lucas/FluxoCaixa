namespace FluxoCaixa.Lancamentos.Dominio;

public readonly record struct ComercianteId
{
    public ComercianteId(string valor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valor);

        Valor = valor;
    }

    public string Valor { get; }

    public override string ToString() => Valor;
}
