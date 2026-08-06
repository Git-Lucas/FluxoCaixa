using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluxoCaixa.Identidade.Testes.Api.Infraestrutura;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FluxoCaixa.Identidade.Testes.Api;

public sealed class EndpointsDeDescobertaTestes : IClassFixture<IdentidadeApiTestesFactory>
{
    private readonly HttpClient _cliente;

    public EndpointsDeDescobertaTestes(IdentidadeApiTestesFactory fabrica)
    {
        _cliente = fabrica.CreateClient();
    }

    private async Task<string> ObterCredencialAsync()
    {
        var credenciais = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{IdentidadeApiTestesFactory.ClientIdValido}:{IdentidadeApiTestesFactory.ClientSecretValido}"));

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/connect/token")
        {
            Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("grant_type", "client_credentials")]),
            Headers = { Authorization = new AuthenticationHeaderValue("Basic", credenciais) },
        };

        var resposta = await _cliente.SendAsync(requisicao);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        return corpo.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("access_token ausente na resposta.");
    }

    [Fact]
    public async Task Jwks_ChavePublicada_VerificaAssinaturaDeCredencialEmitida()
    {
        var token = await ObterCredencialAsync();

        var respostaJwks = await _cliente.GetAsync("/.well-known/jwks.json");
        var jwks = await respostaJwks.Content.ReadFromJsonAsync<JsonElement>();
        var chave = jwks.GetProperty("keys")[0];

        var chaveRsa = new RsaSecurityKey(new RSAParameters
        {
            Modulus = Base64UrlEncoder.DecodeBytes(chave.GetProperty("n").GetString()),
            Exponent = Base64UrlEncoder.DecodeBytes(chave.GetProperty("e").GetString()),
        });

        var parametrosDeValidacao = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "fluxocaixa",
            ValidateAudience = true,
            ValidAudience = "fluxocaixa",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = chaveRsa,
        };

        var principal = new JwtSecurityTokenHandler().ValidateToken(token, parametrosDeValidacao, out _);
        Assert.NotNull(principal);
    }

    [Fact]
    public async Task Jwks_NenhumaSuperficieExpoeChavePrivada()
    {
        var resposta = await _cliente.GetAsync("/.well-known/jwks.json");
        var corpo = await resposta.Content.ReadAsStringAsync();

        Assert.DoesNotContain("\"d\":", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE KEY", corpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Descoberta_TrazOrigemEEnderecoDoJwks()
    {
        var resposta = await _cliente.GetAsync("/.well-known/openid-configuration");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("fluxocaixa", corpo.GetProperty("issuer").GetString());
        Assert.EndsWith("/.well-known/jwks.json", corpo.GetProperty("jwks_uri").GetString(), StringComparison.Ordinal);
        Assert.EndsWith("/connect/token", corpo.GetProperty("token_endpoint").GetString(), StringComparison.Ordinal);
    }
}
