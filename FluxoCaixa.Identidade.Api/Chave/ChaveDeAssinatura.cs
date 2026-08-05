using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FluxoCaixa.Identidade.Api.Chave;

/// <summary>
/// O par de chaves RSA do emissor, com o <c>kid</c> derivado do thumbprint (RFC 7638) — estável
/// entre reinícios, porque depende só do material da chave, nunca de um contador ou de um relógio.
/// </summary>
public sealed class ChaveDeAssinatura : IDisposable
{
    public RSA Rsa { get; }

    public string Kid { get; }

    private ChaveDeAssinatura(RSA rsa, string kid)
    {
        Rsa = rsa;
        Kid = kid;
    }

    /// <summary>
    /// Carrega a chave do arquivo indicado; se ele não existir, gera um novo par de 2048 bits e o
    /// grava ali antes de retornar — a mesma chave é reaproveitada em reinícios subsequentes.
    /// </summary>
    public static ChaveDeAssinatura CarregarOuGerar(string caminhoDoArquivo)
    {
        var rsa = CarregadorDeChaveRsa.CarregarOuGerar(caminhoDoArquivo);
        return new ChaveDeAssinatura(rsa, DerivarKid(rsa));
    }

    private static string DerivarKid(RSA rsa)
    {
        var parametros = rsa.ExportParameters(includePrivateParameters: false);

        // Forma canônica do RFC 7638: apenas os membros que definem a chave, em ordem lexicográfica.
        var n = Base64UrlEncoder.Encode(parametros.Modulus);
        var e = Base64UrlEncoder.Encode(parametros.Exponent);
        var jwkCanonico = $$"""{"e":"{{e}}","kty":"RSA","n":"{{n}}"}""";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(jwkCanonico));
        return Base64UrlEncoder.Encode(hash);
    }

    public void Dispose() => Rsa.Dispose();
}
