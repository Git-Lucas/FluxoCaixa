using FluxoCaixa.Identidade.Api.Chave;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace FluxoCaixa.Identidade.Testes.Chave;

public sealed class AptidaoDoEmissorHealthCheckTestes
{
    [Fact]
    public async Task CheckHealthAsync_ComChaveCarregada_RespondeSaudavel()
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"aptidao-emissor-testes-{Guid.NewGuid():N}.pem");
        using var chave = ChaveDeAssinatura.CarregarOuGerar(caminho);
        try
        {
            var verificacao = new AptidaoDoEmissorHealthCheck(chave);

            var resultado = await verificacao.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, resultado.Status);
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    [Fact]
    public async Task CheckHealthAsync_ComParDeChavesIndisponivel_RespondeInsaudavel()
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"aptidao-emissor-testes-{Guid.NewGuid():N}.pem");
        var chave = ChaveDeAssinatura.CarregarOuGerar(caminho);
        try
        {
            chave.Rsa.Dispose();

            var verificacao = new AptidaoDoEmissorHealthCheck(chave);

            var resultado = await verificacao.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, resultado.Status);
        }
        finally
        {
            File.Delete(caminho);
        }
    }
}
