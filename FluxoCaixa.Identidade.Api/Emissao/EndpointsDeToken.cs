using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluxoCaixa.Identidade.Api.Chave;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FluxoCaixa.Identidade.Api.Emissao;

internal static class EndpointsDeToken
{
    public const string Caminho = "/connect/token";

    public static void MapearEndpointDeToken(this IEndpointRouteBuilder app)
    {
        app.MapPost(Caminho, EmitirAsync);
    }

    private static async Task<IResult> EmitirAsync(
        HttpContext httpContext,
        IOptions<OpcoesDoEmissor> opcoesDoEmissor,
        ChaveDeAssinatura chaveDeAssinatura,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.HasFormContentType)
        {
            return ErroDoProtocolo(StatusCodes.Status400BadRequest, "invalid_request");
        }

        var formulario = await httpContext.Request.ReadFormAsync(cancellationToken);
        var grantType = formulario["grant_type"].ToString();

        if (string.IsNullOrEmpty(grantType))
        {
            return ErroDoProtocolo(StatusCodes.Status400BadRequest, "invalid_request");
        }

        if (grantType != "client_credentials")
        {
            return ErroDoProtocolo(StatusCodes.Status400BadRequest, "unsupported_grant_type");
        }

        if (!TentarLerCredenciaisBasic(httpContext.Request, out var clientId, out var clientSecret))
        {
            return ErroDeClienteInvalido(httpContext);
        }

        var opcoes = opcoesDoEmissor.Value;
        if (!ClienteAutenticado(opcoes.Clientes, clientId, clientSecret))
        {
            return ErroDeClienteInvalido(httpContext);
        }

        return Results.Json(EmitirCredencial(opcoes, clientId, chaveDeAssinatura));
    }

    private static object EmitirCredencial(OpcoesDoEmissor opcoes, string clientId, ChaveDeAssinatura chaveDeAssinatura)
    {
        var agora = DateTime.UtcNow;
        var credenciais = new SigningCredentials(
            new RsaSecurityKey(chaveDeAssinatura.Rsa) { KeyId = chaveDeAssinatura.Kid },
            SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            opcoes.Emissor,
            opcoes.Audiencia,
            [new Claim(JwtRegisteredClaimNames.Sub, clientId)],
            notBefore: agora,
            expires: agora.Add(opcoes.Validade),
            signingCredentials: credenciais);

        var tokenSerializado = new JwtSecurityTokenHandler().WriteToken(token);

        return new
        {
            access_token = tokenSerializado,
            token_type = "Bearer",
            expires_in = (int)opcoes.Validade.TotalSeconds,
        };
    }

    private static bool ClienteAutenticado(IReadOnlyList<ClienteConfigurado> clientes, string clientId, string clientSecretApresentado)
    {
        var apresentadoBytes = Encoding.UTF8.GetBytes(clientSecretApresentado);
        var clienteConfigurado = clientes.FirstOrDefault(cliente => cliente.ClientId == clientId);

        var configuradoBytes = clienteConfigurado is not null
            ? Encoding.UTF8.GetBytes(clienteConfigurado.ClientSecret)
            : new byte[apresentadoBytes.Length];

        if (configuradoBytes.Length != apresentadoBytes.Length)
        {
            configuradoBytes = new byte[apresentadoBytes.Length];
        }

        var segredoConfere = CryptographicOperations.FixedTimeEquals(apresentadoBytes, configuradoBytes);
        return clienteConfigurado is not null && segredoConfere;
    }

    private static bool TentarLerCredenciaisBasic(HttpRequest request, out string clientId, out string clientSecret)
    {
        clientId = string.Empty;
        clientSecret = string.Empty;

        var cabecalho = request.Headers.Authorization.ToString();
        if (!cabecalho.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        byte[] bytesDecodificados;
        try
        {
            bytesDecodificados = Convert.FromBase64String(cabecalho["Basic ".Length..].Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        var texto = Encoding.UTF8.GetString(bytesDecodificados);
        var indiceDoisPontos = texto.IndexOf(':', StringComparison.Ordinal);
        if (indiceDoisPontos < 0)
        {
            return false;
        }

        clientId = Uri.UnescapeDataString(texto[..indiceDoisPontos]);
        clientSecret = Uri.UnescapeDataString(texto[(indiceDoisPontos + 1)..]);
        return true;
    }

    private static IResult ErroDeClienteInvalido(HttpContext httpContext)
    {
        httpContext.Response.Headers.WWWAuthenticate = "Basic";
        return ErroDoProtocolo(StatusCodes.Status401Unauthorized, "invalid_client");
    }

    private static IResult ErroDoProtocolo(int statusCode, string erro)
        => Results.Json(new { error = erro }, statusCode: statusCode);
}
