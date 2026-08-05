using System.Security.Cryptography;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FluxoCaixa.Identidade.Api.Chave;

internal sealed class AptidaoDoEmissorHealthCheck(ChaveDeAssinatura chaveDeAssinatura) : IHealthCheck
{
    private readonly ChaveDeAssinatura _chaveDeAssinatura = chaveDeAssinatura;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            _chaveDeAssinatura.Rsa.ExportParameters(includePrivateParameters: false);
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception excecao) when (excecao is CryptographicException or ObjectDisposedException)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy());
        }
    }
}
