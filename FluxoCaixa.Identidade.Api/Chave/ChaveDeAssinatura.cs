using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FluxoCaixa.Identidade.Api.Chave;

public sealed class ChaveDeAssinatura : IDisposable
{
    public RSA Rsa { get; }

    public string Kid { get; }

    private ChaveDeAssinatura(RSA rsa, string kid)
    {
        Rsa = rsa;
        Kid = kid;
    }

    public static ChaveDeAssinatura CarregarOuGerar(string caminhoDoArquivo)
    {
        var rsa = CarregadorDeChaveRsa.CarregarOuGerar(caminhoDoArquivo);
        return new ChaveDeAssinatura(rsa, DerivarKid(rsa));
    }

    private static string DerivarKid(RSA rsa)
    {
        var parametros = rsa.ExportParameters(includePrivateParameters: false);

        var n = Base64UrlEncoder.Encode(parametros.Modulus);
        var e = Base64UrlEncoder.Encode(parametros.Exponent);
        var jwkCanonico = $$"""{"e":"{{e}}","kty":"RSA","n":"{{n}}"}""";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(jwkCanonico));
        return Base64UrlEncoder.Encode(hash);
    }

    public void Dispose() => Rsa.Dispose();
}
