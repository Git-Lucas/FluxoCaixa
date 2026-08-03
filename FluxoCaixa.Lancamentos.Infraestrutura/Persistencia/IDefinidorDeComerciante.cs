using FluxoCaixa.Lancamentos.Dominio;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;

/// <summary>
/// Porta pela qual a borda (a única chamadora legítima) alimenta o comerciante da requisição
/// corrente a partir da credencial validada. Separada de <see cref="Aplicacao.Portas.IContextoComerciante"/>
/// porque só a borda escreve; o caso de uso só lê.
/// </summary>
public interface IDefinidorDeComerciante
{
    void Definir(ComercianteId comercianteId);
}
