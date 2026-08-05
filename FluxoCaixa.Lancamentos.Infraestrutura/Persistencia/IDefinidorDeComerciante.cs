using FluxoCaixa.Lancamentos.Dominio;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;

public interface IDefinidorDeComerciante
{
    void Definir(ComercianteId comercianteId);
}
