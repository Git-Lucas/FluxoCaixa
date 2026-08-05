using FluxoCaixa.Identidade.Api.Chave;
using FluxoCaixa.Identidade.Api.Emissao;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FluxoCaixa.Identidade.Api.Descoberta;

internal static class EndpointsDeDescoberta
{
    public const string CaminhoDoJwks = "/.well-known/jwks.json";
    private const string _caminhoDaConfiguracao = "/.well-known/openid-configuration";

    public static void MapearEndpointsDeDescoberta(this IEndpointRouteBuilder app)
    {
        app.MapGet(CaminhoDoJwks, ObterJwks);
        app.MapGet(_caminhoDaConfiguracao, ObterConfiguracao);
    }

    private static IResult ObterJwks(ChaveDeAssinatura chaveDeAssinatura)
    {
        var parametros = chaveDeAssinatura.Rsa.ExportParameters(includePrivateParameters: false);

        var chave = new
        {
            kty = "RSA",
            use = "sig",
            alg = "RS256",
            kid = chaveDeAssinatura.Kid,
            n = Base64UrlEncoder.Encode(parametros.Modulus),
            e = Base64UrlEncoder.Encode(parametros.Exponent),
        };

        return Results.Json(new { keys = new[] { chave } });
    }

    private static IResult ObterConfiguracao(HttpContext httpContext, IOptions<OpcoesDoEmissor> opcoesDoEmissor)
    {
        var enderecoBase = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";

        var documento = new
        {
            issuer = opcoesDoEmissor.Value.Emissor,
            token_endpoint = $"{enderecoBase}{EndpointsDeToken.Caminho}",
            jwks_uri = $"{enderecoBase}{CaminhoDoJwks}",
            grant_types_supported = new[] { "client_credentials" },
            token_endpoint_auth_methods_supported = new[] { "client_secret_basic" },
            id_token_signing_alg_values_supported = new[] { "RS256" },
        };

        return Results.Json(documento);
    }
}
