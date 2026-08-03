namespace FluxoCaixa.Lancamentos.Dominio;

/// <summary>
/// Identifica o comerciante dono do lançamento. É sempre derivado da credencial apresentada,
/// nunca de dado informado na requisição.
/// </summary>
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
