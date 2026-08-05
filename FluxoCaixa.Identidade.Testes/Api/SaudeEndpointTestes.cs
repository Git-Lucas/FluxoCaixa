using System.Net;
using FluxoCaixa.Identidade.Testes.Api.Infraestrutura;
using Xunit;

namespace FluxoCaixa.Identidade.Testes.Api;

public sealed class SaudeEndpointTestes : IClassFixture<IdentidadeApiTestesFactory>
{
    private readonly HttpClient _cliente;

    public SaudeEndpointTestes(IdentidadeApiTestesFactory fabrica)
    {
        _cliente = fabrica.CreateClient();
    }

    [Fact]
    public async Task Saude_ComParDeChavesCarregado_HealthyEReadyRespondemSucesso()
    {
        var healthy = await _cliente.GetAsync("/health/healthy");
        var ready = await _cliente.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, healthy.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public async Task Saude_RespostaNaoExpoeDetalheInterno()
    {
        var ready = await _cliente.GetAsync("/health/ready");
        var corpo = await ready.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Exception", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("chave-de-assinatura.pem", corpo, StringComparison.Ordinal);
    }
}
