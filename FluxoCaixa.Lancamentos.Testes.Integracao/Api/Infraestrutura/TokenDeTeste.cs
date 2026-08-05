using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace FluxoCaixa.Lancamentos.Api.Testes.Infraestrutura;

internal static class TokenDeTeste
{
    private const string _emissor = "fluxocaixa";
    private const string _audiencia = "fluxocaixa";

    private static readonly RSA _chave = RSA.Create(2048);

    public static RSA ChavePublica { get; } = RSA.Create(_chave.ExportParameters(includePrivateParameters: false));

    public static string Gerar(string comercianteId, TimeSpan? validoPor = null, RSA? chaveDeAssinatura = null)
    {
        var credenciais = new SigningCredentials(new RsaSecurityKey(chaveDeAssinatura ?? _chave), SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            _emissor,
            _audiencia,
            [new Claim("sub", comercianteId)],
            expires: DateTime.UtcNow.Add(validoPor ?? TimeSpan.FromMinutes(5)),
            signingCredentials: credenciais);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
