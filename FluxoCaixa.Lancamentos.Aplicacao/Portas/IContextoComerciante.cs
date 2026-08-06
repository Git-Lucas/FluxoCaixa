using FluxoCaixa.Lancamentos.Dominio;

namespace FluxoCaixa.Lancamentos.Aplicacao.Portas;

public interface IContextoComerciante
{
    ComercianteId ComercianteId { get; }
}
