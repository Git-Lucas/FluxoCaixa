namespace FluxoCaixa.Identidade.Api.Chave;

public sealed class OpcoesDaChave
{
    public const string SecaoDeConfiguracao = "Chave";

    public string CaminhoDoArquivo { get; init; } = "/dados/chave/chave-de-assinatura.pem";
}
