using FluxoCaixa.Lancamentos.Aplicacao.Portas;
using FluxoCaixa.Lancamentos.Dominio;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;

internal sealed class ContextoComerciante : IContextoComerciante, IDefinidorDeComerciante
{
    private ComercianteId? _comercianteId;

    public ComercianteId ComercianteId
        => _comercianteId ?? throw new InvalidOperationException(
            "O comerciante da requisição ainda não foi definido a partir da credencial.");

    public void Definir(ComercianteId comercianteId)
    {
        if (_comercianteId is not null)
        {
            throw new InvalidOperationException("O comerciante da requisição já foi definido.");
        }

        _comercianteId = comercianteId;
    }
}
