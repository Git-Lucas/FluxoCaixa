namespace FluxoCaixa.Consolidado.Api.Persistencia;

/// <summary>
/// Comerciante da operação corrente. No caminho autenticado, vem da credencial; no consumo do
/// evento, vem da mensagem — a mesma proteção estrutural, com origem diferente.
/// </summary>
public interface IContextoComerciante
{
    string ComercianteId { get; }
}
