using FluxoCaixa.Lancamentos.Aplicacao;
using FluxoCaixa.Lancamentos.Dominio;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FluxoCaixa.Lancamentos.Api.Erros;

internal sealed class ExcecaoDeDominioParaProblemDetails : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            LancamentoInvalidoException lancamentoInvalido => new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = lancamentoInvalido.Regra.ToString(),
                Type = $"https://fluxocaixa.dev/erros/{lancamentoInvalido.Regra}",
                Detail = lancamentoInvalido.Message,
            },
            ConflitoDeConteudoIdempotenteException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "ConflitoDeConteudoIdempotente",
                Type = "https://fluxocaixa.dev/erros/conflito-de-conteudo-idempotente",
                Detail = "A chave de idempotência já foi usada com um conteúdo diferente do informado agora.",
            },
            _ => null,
        };

        if (problemDetails is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
