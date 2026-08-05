using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluxoCaixa.Identidade.Testes.Api.Infraestrutura;
using Xunit;

namespace FluxoCaixa.Identidade.Testes.Api;

public sealed class AusenciaDeSegredoNoLogTestes : IClassFixture<IdentidadeApiTestesFactory>
{
    private readonly IdentidadeApiTestesFactory _fabrica;
    private readonly HttpClient _cliente;

    public AusenciaDeSegredoNoLogTestes(IdentidadeApiTestesFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    private static HttpRequestMessage CriarRequisicao(string clientId, string clientSecret)
    {
        var credenciais = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        return new HttpRequestMessage(HttpMethod.Post, "/connect/token")
        {
            Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("grant_type", "client_credentials")]),
            Headers = { Authorization = new AuthenticationHeaderValue("Basic", credenciais) },
        };
    }

    [Fact]
    public async Task Emitir_EmissaoBemSucedida_NenhumLogContemOSegredoOuACredencial()
    {
        const string segredo = "segredo-que-nao-pode-vazar-no-log-9f8e7d";
        using var requisicao = CriarRequisicao(IdentidadeApiTestesFactory.ClientIdValido, segredo);

        await _cliente.SendAsync(requisicao);

        using var requisicaoValida = CriarRequisicao(IdentidadeApiTestesFactory.ClientIdValido, IdentidadeApiTestesFactory.ClientSecretValido);
        var resposta = await _cliente.SendAsync(requisicaoValida);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        var token = corpo.GetProperty("access_token").GetString()!;

        var mensagens = _fabrica.CapturadorDeLog.Mensagens;
        Assert.DoesNotContain(mensagens, mensagem => mensagem.Contains(segredo, StringComparison.Ordinal));
        Assert.DoesNotContain(mensagens, mensagem => mensagem.Contains(IdentidadeApiTestesFactory.ClientSecretValido, StringComparison.Ordinal));
        Assert.DoesNotContain(mensagens, mensagem => mensagem.Contains(token, StringComparison.Ordinal));
    }
}
