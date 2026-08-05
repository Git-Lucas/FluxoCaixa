using System.Security.Cryptography;

namespace FluxoCaixa.Identidade.Api.Chave;

internal static class CarregadorDeChaveRsa
{
    private const int _tamanhoDaChaveEmBits = 2048;

    public static RSA CarregarOuGerar(string caminhoDoArquivo)
    {
        if (File.Exists(caminhoDoArquivo))
        {
            var chaveExistente = RSA.Create();
            chaveExistente.ImportFromPem(File.ReadAllText(caminhoDoArquivo));
            return chaveExistente;
        }

        var diretorio = Path.GetDirectoryName(caminhoDoArquivo);
        if (!string.IsNullOrEmpty(diretorio))
        {
            Directory.CreateDirectory(diretorio);
        }

        var chaveNova = RSA.Create(_tamanhoDaChaveEmBits);
        File.WriteAllText(caminhoDoArquivo, chaveNova.ExportRSAPrivateKeyPem());
        return chaveNova;
    }
}
