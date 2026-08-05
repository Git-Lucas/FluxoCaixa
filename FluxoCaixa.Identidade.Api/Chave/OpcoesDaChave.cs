namespace FluxoCaixa.Identidade.Api.Chave;

public sealed class OpcoesDaChave
{
    public const string SecaoDeConfiguracao = "Chave";

    /// <summary>
    /// Caminho do arquivo PEM da chave privada. Vem de configuração para que os testes de borda,
    /// que não podem escrever no volume nomeado do compose, apontem para um arquivo temporário.
    /// </summary>
    public string CaminhoDoArquivo { get; init; } = "/dados/chave/chave-de-assinatura.pem";
}
