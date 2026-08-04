namespace FluxoCaixa.Consolidado.Api.Autenticacao;

/// <summary>
/// A validação da credencial é local, contra uma chave configurada — nunca contra o emissor
/// durante o atendimento da requisição. Enquanto o emissor de credenciais (PRD 03) não existe, os
/// testes forjam tokens com uma chave de teste.
/// </summary>
public sealed class OpcoesAutenticacao
{
    public const string SecaoDeConfiguracao = "Autenticacao";

    public required string ChaveDeAssinatura { get; init; }

    public string Emissor { get; init; } = "fluxocaixa";

    public string Audiencia { get; init; } = "fluxocaixa";

    /// <summary>Nome da claim que carrega o identificador do comerciante.</summary>
    public string ClaimDoComerciante { get; init; } = "sub";
}
