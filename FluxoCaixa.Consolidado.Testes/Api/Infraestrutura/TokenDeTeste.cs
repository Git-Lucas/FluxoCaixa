using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FluxoCaixa.Consolidado.Testes.Api.Infraestrutura;

/// <summary>
/// Forja tokens com a mesma chave configurada em appsettings.json para os testes. O emissor de
/// credenciais real (PRD 03) ainda não existe.
/// </summary>
internal static class TokenDeTeste
{
    public const string ChaveDeAssinatura = "troque-esta-chave-de-teste-em-qualquer-ambiente-real-0123456789";
    private const string Emissor = "fluxocaixa";
    private const string Audiencia = "fluxocaixa";

    public static string Gerar(string comercianteId, TimeSpan? validoPor = null, string? chaveDeAssinatura = null)
    {
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chaveDeAssinatura ?? ChaveDeAssinatura));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            Emissor,
            Audiencia,
            [new Claim("sub", comercianteId)],
            expires: DateTime.UtcNow.Add(validoPor ?? TimeSpan.FromMinutes(5)),
            signingCredentials: credenciais);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
