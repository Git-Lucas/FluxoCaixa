namespace FluxoCaixa.Consolidado.Api.Autenticacao;

/// <summary>
/// A validação da credencial é local, contra a chave pública obtida do emissor (<see cref="Authority"/>)
/// fora do caminho da requisição e mantida em cache — nunca uma consulta a ele durante o
/// atendimento. O serviço não guarda material capaz de assinar, só de verificar.
/// </summary>
public sealed class OpcoesAutenticacao
{
    public const string SecaoDeConfiguracao = "Autenticacao";

    /// <summary>Endereço do emissor de credenciais, de onde a chave pública e a descoberta são obtidas.</summary>
    public required string Authority { get; init; }

    public string Emissor { get; init; } = "fluxocaixa";

    public string Audiencia { get; init; } = "fluxocaixa";

    /// <summary>Nome da claim que carrega o identificador do comerciante.</summary>
    public string ClaimDoComerciante { get; init; } = "sub";

    /// <summary>
    /// Desligado apenas no ambiente local do docker-compose, onde o emissor não tem TLS —
    /// inaceitável fora dessa rede.
    /// </summary>
    public bool RequererHttps { get; init; } = true;
}
