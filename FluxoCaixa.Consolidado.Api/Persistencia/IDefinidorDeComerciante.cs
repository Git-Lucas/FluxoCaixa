namespace FluxoCaixa.Consolidado.Api.Persistencia;

/// <summary>
/// Porta pela qual o caminho chamador — a borda autenticada ou o consumidor do evento — alimenta o
/// comerciante da operação corrente. Separada de <see cref="IContextoComerciante"/> porque só o
/// chamador escreve; o resto do processamento só lê.
/// </summary>
public interface IDefinidorDeComerciante
{
    void Definir(string comercianteId);
}
