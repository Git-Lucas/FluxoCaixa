using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluxoCaixa.Consolidado.Api.Consulta;
using FluxoCaixa.Consolidado.Api.Persistencia;
using FluxoCaixa.Consolidado.Testes.Api.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FluxoCaixa.Consolidado.Testes.Api;

public class ConsultaEndpointTestes : IClassFixture<ApiTestesFactory>
{
    private readonly ApiTestesFactory _fabrica;
    private readonly HttpClient _cliente;

    public ConsultaEndpointTestes(ApiTestesFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    [Fact]
    public async Task Consultar_SemCredencial_EhRejeitado()
    {
        var resposta = await _cliente.GetAsync(new Uri("/consolidado/2026-08-02", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Consultar_DataForaDoFormato_EhRejeitadaComQuatroCentos()
    {
        var token = TokenDeTeste.Gerar("comerciante-data-invalida");
        using var requisicao = CriarRequisicao("/consolidado/02-08-2026", token);

        var resposta = await _cliente.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Consultar_ComerciantesDistintos_NaoAlcancaConsolidadoAlheio()
    {
        var comercianteA = $"comerciante-a-{Guid.NewGuid()}";
        var comercianteB = $"comerciante-b-{Guid.NewGuid()}";

        await SemearConsolidadoAsync(comercianteB, new DateOnly(2026, 8, 2), totalCredito: 999m);

        var token = TokenDeTeste.Gerar(comercianteA);
        using var requisicao = CriarRequisicao("/consolidado/2026-08-02", token);

        var resposta = await _cliente.SendAsync(requisicao);
        var corpo = await resposta.Content.ReadFromJsonAsync<ConsolidadoRespostaDto>();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(0m, corpo!.TotalCredito);
    }

    private static HttpRequestMessage CriarRequisicao(string caminho, string? token)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Get, caminho);

        if (token is not null)
        {
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return requisicao;
    }

    private async Task SemearConsolidadoAsync(string comercianteId, DateOnly competencia, decimal totalCredito)
    {
        await using var escopo = _fabrica.Services.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<IDefinidorDeComerciante>().Definir(comercianteId);
        var dbContext = escopo.ServiceProvider.GetRequiredService<ConsolidadoDbContext>();

        dbContext.ConsolidadosDiarios.Add(new ConsolidadoDiario
        {
            ComercianteId = comercianteId,
            Competencia = competencia,
            TotalCredito = totalCredito,
            TotalDebito = 0m,
            AtualizadoEm = DateTimeOffset.UtcNow,
        });

        await dbContext.SaveChangesAsync();
    }
}
