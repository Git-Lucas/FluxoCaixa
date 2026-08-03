using FluxoCaixa.Lancamentos.Dominio;

namespace FluxoCaixa.Lancamentos.Aplicacao.Portas;

/// <summary>
/// Comerciante da requisição corrente, determinado exclusivamente pela credencial apresentada.
/// </summary>
public interface IContextoComerciante
{
    ComercianteId ComercianteId { get; }
}
