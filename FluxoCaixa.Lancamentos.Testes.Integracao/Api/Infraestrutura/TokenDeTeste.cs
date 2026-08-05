using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace FluxoCaixa.Lancamentos.Api.Testes.Infraestrutura;

/// <summary>
/// Forja tokens com um par RSA efêmero gerado nesta execução — o host de teste
/// (<see cref="ApiTestesFactory"/>) injeta a chave pública correspondente diretamente na validação,
/// sem subir o emissor real.
/// </summary>
internal static class TokenDeTeste
{
    private const string Emissor = "fluxocaixa";
    private const string Audiencia = "fluxocaixa";

    private static readonly RSA s_chave = RSA.Create(2048);

    public static RSA ChavePublica { get; } = RSA.Create(s_chave.ExportParameters(includePrivateParameters: false));

    public static string Gerar(string comercianteId, TimeSpan? validoPor = null, RSA? chaveDeAssinatura = null)
    {
        var credenciais = new SigningCredentials(new RsaSecurityKey(chaveDeAssinatura ?? s_chave), SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            Emissor,
            Audiencia,
            [new Claim("sub", comercianteId)],
            expires: DateTime.UtcNow.Add(validoPor ?? TimeSpan.FromMinutes(5)),
            signingCredentials: credenciais);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
