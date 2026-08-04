namespace FluxoCaixa.Consolidado.Api.Persistencia;

/// <summary>
/// Comerciante da operação corrente. Escopo de requisição/consumo (registrado como serviço
/// "scoped"), só pode ser definido uma vez, e só pela credencial validada ou pela mensagem
/// consumida — nunca por outro dado da requisição.
/// </summary>
internal sealed class ContextoComerciante : IContextoComerciante, IDefinidorDeComerciante
{
    private string? _comercianteId;

    public string ComercianteId
        => _comercianteId ?? throw new InvalidOperationException(
            "O comerciante da operação ainda não foi definido.");

    public void Definir(string comercianteId)
    {
        if (_comercianteId is not null)
        {
            throw new InvalidOperationException("O comerciante da operação já foi definido.");
        }

        _comercianteId = comercianteId;
    }
}
