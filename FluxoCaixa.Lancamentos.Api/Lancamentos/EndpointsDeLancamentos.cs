using FluxoCaixa.Lancamentos.Api.Contratos;
using FluxoCaixa.Lancamentos.Aplicacao.Portas;
using FluxoCaixa.Lancamentos.Aplicacao.RegistrarLancamento;
using FluxoCaixa.Lancamentos.Dominio;
using FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;
using FluxoCaixa.Plataforma.Autenticacao;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FluxoCaixa.Lancamentos.Api.Lancamentos;

internal static class EndpointsDeLancamentos
{
    private const string _nomeDoCabecalhoDaChave = "Idempotency-Key";
    private const int _tamanhoMaximoDaChave = 64;

    public static void MapearEndpointsDeLancamentos(this IEndpointRouteBuilder app)
    {
        app.MapPost("/lancamentos", RegistrarAsync)
            .RequireAuthorization();
    }

    private static async Task<IResult> RegistrarAsync(
        HttpContext httpContext,
        RegistrarLancamentoRequisicaoDto corpo,
        RegistrarLancamentoCasoDeUso casoDeUso,
        IDefinidorDeComerciante definidorDeComerciante,
        IOptions<OpcoesAutenticacao> opcoesAutenticacao,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Headers.TryGetValue(_nomeDoCabecalhoDaChave, out var valores) || valores.Count != 1)
        {
            return ProblemaDeChaveAusente();
        }

        var chave = valores[0];
        if (!ChaveEhValida(chave))
        {
            return ProblemaDeChaveInvalida();
        }

        var comercianteId = httpContext.User.FindFirst(opcoesAutenticacao.Value.ClaimDoComerciante)?.Value;
        if (string.IsNullOrWhiteSpace(comercianteId))
        {
            return ProblemaDeCredencialSemComerciante();
        }

        definidorDeComerciante.Definir(new ComercianteId(comercianteId));

        var requisicao = new RegistrarLancamentoRequisicao(chave!, corpo.Tipo, corpo.Valor, corpo.Competencia, corpo.Descricao);
        var resposta = await casoDeUso.ExecutarAsync(requisicao, cancellationToken);

        var dto = new RegistrarLancamentoRespostaDto(resposta.LancamentoId, resposta.RecebidoEm, resposta.Criado);
        var statusCode = resposta.Criado ? StatusCodes.Status201Created : StatusCodes.Status200OK;

        return Results.Json(dto, statusCode: statusCode);
    }

    private static bool ChaveEhValida(string? chave)
        => !string.IsNullOrEmpty(chave)
           && chave.Length <= _tamanhoMaximoDaChave
           && chave.All(caractere => !char.IsControl(caractere));

    private static IResult ProblemaDeChaveAusente() => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "ChaveDeIdempotenciaAusente",
        type: "https://fluxocaixa.dev/erros/chave-de-idempotencia-ausente",
        detail: $"O cabeçalho '{_nomeDoCabecalhoDaChave}' é obrigatório e deve aparecer uma única vez.");

    private static IResult ProblemaDeChaveInvalida() => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "ChaveDeIdempotenciaInvalida",
        type: "https://fluxocaixa.dev/erros/chave-de-idempotencia-invalida",
        detail: $"A chave de idempotência deve ser não vazia, com no máximo {_tamanhoMaximoDaChave} caracteres imprimíveis.");

    private static IResult ProblemaDeCredencialSemComerciante() => Results.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "CredencialSemComerciante",
        type: "https://fluxocaixa.dev/erros/credencial-sem-comerciante",
        detail: "A credencial apresentada não identifica um comerciante.");
}
