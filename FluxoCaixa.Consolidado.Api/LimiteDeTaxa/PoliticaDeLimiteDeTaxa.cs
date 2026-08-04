using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FluxoCaixa.Consolidado.Api.LimiteDeTaxa;

/// <summary>
/// Uma única política, particionada pelo estado de autenticação: comerciantes autenticados usam o
/// limite de 100 req/s (rajada 200); tráfego sem credencial válida — ao qual o limite por
/// comerciante não alcança — usa o limite de 20 req/s (rajada 40) por endereço de origem.
/// </summary>
internal static class PoliticaDeLimiteDeTaxa
{
    public const string Nome = "padrao";

    public static void Configurar(RateLimiterOptions opcoes, string claimDoComerciante)
    {
        opcoes.OnRejected = TratarRejeicaoAsync;

        opcoes.AddPolicy(Nome, httpContext => httpContext.User.Identity?.IsAuthenticated == true
            ? ParticaoPorComerciante(httpContext, claimDoComerciante)
            : ParticaoPorOrigemNaoAutenticada(httpContext));
    }

    private static RateLimitPartition<string> ParticaoPorComerciante(HttpContext httpContext, string claimDoComerciante)
    {
        var comercianteId = httpContext.User.FindFirst(claimDoComerciante)?.Value ?? "comerciante-desconhecido";

        return RateLimitPartition.Get(comercianteId, _ => CriarLimitadorDeTokens(tokenLimit: 200, tokensPerPeriod: 100));
    }

    private static RateLimitPartition<string> ParticaoPorOrigemNaoAutenticada(HttpContext httpContext)
    {
        var enderecoDeOrigem = httpContext.Connection.RemoteIpAddress?.ToString() ?? "origem-desconhecida";

        return RateLimitPartition.Get(enderecoDeOrigem, _ => CriarLimitadorDeTokens(tokenLimit: 40, tokensPerPeriod: 20));
    }

    /// <summary>
    /// Construído diretamente, em vez de via <c>RateLimitPartition.GetTokenBucketLimiter</c>: esse
    /// helper força <c>AutoReplenishment = false</c> e depende de reposição externa que, nesta
    /// versão do runtime, não ocorre — o limite nunca rejeita nenhuma requisição. Confirmado por
    /// teste de carga direto comparando as duas formas. Construir o limitador diretamente, com seu
    /// próprio temporizador de reposição, contorna o problema.
    /// </summary>
    private static TokenBucketRateLimiter CriarLimitadorDeTokens(int tokenLimit, int tokensPerPeriod)
        => new(new TokenBucketRateLimiterOptions
        {
            TokenLimit = tokenLimit,
            TokensPerPeriod = tokensPerPeriod,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true,
            QueueLimit = 0,
        });

    private static async ValueTask TratarRejeicaoAsync(OnRejectedContext contexto, CancellationToken cancellationToken)
    {
        contexto.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        var tempoDeEsperaEmSegundos = contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? (int)retryAfter.TotalSeconds
            : 1;

        contexto.HttpContext.Response.Headers.RetryAfter = tempoDeEsperaEmSegundos.ToString(CultureInfo.InvariantCulture);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "LimiteDeRequisicoesExcedido",
            Type = "https://fluxocaixa.dev/erros/limite-de-requisicoes-excedido",
            Detail = $"Limite de requisições excedido. Tente novamente em {tempoDeEsperaEmSegundos}s.",
        };

        await contexto.HttpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
    }
}
