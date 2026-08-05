using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluxoCaixa.Identidade.Testes.Api.Infraestrutura;
using Xunit;

namespace FluxoCaixa.Identidade.Testes.Api;

public sealed class EndpointDeTokenTestes : IClassFixture<IdentidadeApiTestesFactory>
{
    private const string _caminho = "/connect/token";

    private readonly HttpClient _cliente;

    public EndpointDeTokenTestes(IdentidadeApiTestesFactory fabrica)
    {
        _cliente = fabrica.CreateClient();
    }

    private static HttpRequestMessage CriarRequisicao(string? clientId, string? clientSecret, string? grantType = "client_credentials")
    {
        var campos = new List<KeyValuePair<string, string>>();
        if (grantType is not null)
        {
            campos.Add(new KeyValuePair<string, string>("grant_type", grantType));
        }

        var requisicao = new HttpRequestMessage(HttpMethod.Post, _caminho)
        {
            Content = new FormUrlEncodedContent(campos),
        };

        if (clientId is not null && clientSecret is not null)
        {
            var credenciais = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Basic", credenciais);
        }

        return requisicao;
    }

    [Fact]
    public async Task Emitir_ClienteConhecidoESegredoCorreto_EmiteCredencial()
    {
        using var requisicao = CriarRequisicao(IdentidadeApiTestesFactory.ClientIdValido, IdentidadeApiTestesFactory.ClientSecretValido);

        var resposta = await _cliente.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(corpo.GetProperty("access_token").GetString()));
        Assert.Equal("Bearer", corpo.GetProperty("token_type").GetString());
        Assert.True(corpo.GetProperty("expires_in").GetInt32() > 0);
    }

    [Fact]
    public async Task Emitir_SegredoIncorreto_EhRejeitadoComErroDoProtocolo()
    {
        using var requisicao = CriarRequisicao(IdentidadeApiTestesFactory.ClientIdValido, "segredo-errado");

        var resposta = await _cliente.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_client", corpo.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Emitir_ClienteInexistente_EhRejeitadoComAMesmaRespostaDoSegredoIncorreto()
    {
        using var requisicao = CriarRequisicao("cliente-que-nao-existe", "qualquer-segredo");

        var resposta = await _cliente.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_client", corpo.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Emitir_SemAutenticacaoDeCliente_EhRejeitado()
    {
        using var requisicao = CriarRequisicao(clientId: null, clientSecret: null);

        var resposta = await _cliente.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_client", corpo.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Emitir_GrantTypeNaoSuportado_EhRejeitado()
    {
        using var requisicao = CriarRequisicao(
            IdentidadeApiTestesFactory.ClientIdValido,
            IdentidadeApiTestesFactory.ClientSecretValido,
            grantType: "password");

        var resposta = await _cliente.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unsupported_grant_type", corpo.GetProperty("error").GetString());
    }
}
