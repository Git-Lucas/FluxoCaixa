using System.Globalization;
using FluxoCaixa.Consolidado.Api.Persistencia;
using FluxoCaixa.Plataforma.Autenticacao;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FluxoCaixa.Consolidado.Api.Consulta;

internal static class EndpointsDeConsulta
{
    private const string _formatoDaData = "yyyy-MM-dd";

    public static void MapearEndpointsDeConsulta(this IEndpointRouteBuilder app)
    {
        app.MapGet("/consolidado/{data}", ConsultarAsync)
            .RequireAuthorization();
    }

    private static async Task<IResult> ConsultarAsync(
        HttpContext httpContext,
        string data,
        ConsolidadoDbContext dbContext,
        IDefinidorDeComerciante definidorDeComerciante,
        IOptions<OpcoesAutenticacao> opcoesAutenticacao,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(data, _formatoDaData, CultureInfo.InvariantCulture, DateTimeStyles.None, out var competencia))
        {
            return ProblemaDeDataInvalida();
        }

        var comercianteId = httpContext.User.FindFirst(opcoesAutenticacao.Value.ClaimDoComerciante)?.Value;
        if (string.IsNullOrWhiteSpace(comercianteId))
        {
            return ProblemaDeCredencialSemComerciante();
        }

        definidorDeComerciante.Definir(comercianteId);

        var consolidado = await dbContext.ConsolidadosDiarios
            .AsNoTracking()
            .SingleOrDefaultAsync(consolidado => consolidado.Competencia == competencia, cancellationToken);

        return Results.Ok(ConsolidadoRespostaDto.Compor(consolidado));
    }

    private static IResult ProblemaDeDataInvalida() => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "DataInvalida",
        type: "https://fluxocaixa.dev/erros/data-invalida",
        detail: $"A data deve estar no formato '{_formatoDaData}'.");

    private static IResult ProblemaDeCredencialSemComerciante() => Results.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "CredencialSemComerciante",
        type: "https://fluxocaixa.dev/erros/credencial-sem-comerciante",
        detail: "A credencial apresentada não identifica um comerciante.");
}
