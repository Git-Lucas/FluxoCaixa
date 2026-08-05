using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluxoCaixa.Identidade.Testes.Api.Infraestrutura;
using Xunit;

namespace FluxoCaixa.Identidade.Testes.Api;

public sealed class LimiteDeTaxaTestes : IClassFixture<IdentidadeApiTestesFactory>
{
    private readonly HttpClient _cliente;

    public LimiteDeTaxaTestes(IdentidadeApiTestesFactory fabrica)
    {
        _cliente = fabrica.CreateClient();
    }

    [Fact]
    public async Task Emitir_ExcedendoOLimitePorOrigem_RespondeComRetryAfterEProblemDetails()
    {
        HttpResponseMessage? respostaLimitada = null;

        for (var tentativa = 0; tentativa < 30 && respostaLimitada is null; tentativa++)
        {
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/connect/token")
            {
                Content = new FormUrlEncodedContent([]),
            };

            var resposta = await _cliente.SendAsync(requisicao);
            if (resposta.StatusCode == HttpStatusCode.TooManyRequests)
            {
                respostaLimitada = resposta;
            }
        }

        Assert.NotNull(respostaLimitada);
        Assert.True(respostaLimitada.Headers.RetryAfter is not null);

        var corpo = await respostaLimitada.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)HttpStatusCode.TooManyRequests, corpo.GetProperty("status").GetInt32());
    }
}
