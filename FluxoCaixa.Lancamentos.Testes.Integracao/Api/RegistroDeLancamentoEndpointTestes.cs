using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using FluxoCaixa.Lancamentos.Api.Contratos;
using FluxoCaixa.Lancamentos.Api.Testes.Infraestrutura;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FluxoCaixa.Lancamentos.Api.Testes;

public class RegistroDeLancamentoEndpointTestes : IClassFixture<ApiTestesFactory>
{
    private const string _cabecalhoDaChave = "Idempotency-Key";

    private readonly ApiTestesFactory _fabrica;
    private readonly HttpClient _cliente;

    public RegistroDeLancamentoEndpointTestes(ApiTestesFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    private static object CorpoValido() => new
    {
        tipo = "credito",
        valor = 100m,
        competencia = "2026-08-02",
        descricao = "venda de balcão",
    };

    private HttpRequestMessage CriarRequisicao(object corpo, string? token, string? chave = "chave-1")
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/lancamentos")
        {
            Content = JsonContent.Create(corpo),
        };

        if (token is not null)
        {
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (chave is not null)
        {
            requisicao.Headers.Add(_cabecalhoDaChave, chave);
        }

        return requisicao;
    }

    [Fact]
    public async Task Registrar_SemCredencial_EhRejeitadoENadaEhPersistido()
    {
        var contagemAntes = _fabrica.Armazenamento.Lancamentos.Count;
        using var requisicao = CriarRequisicao(CorpoValido(), token: null, chave: "chave-sem-credencial");

        var resposta = await _cliente.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal(contagemAntes, _fabrica.Armazenamento.Lancamentos.Count);
    }

    [Fact]
    public async Task Registrar_CredencialExpirada_EhRejeitado()
    {
        var token = TokenDeTeste.Gerar("comerciante-1", validoPor: TimeSpan.FromMinutes(-10));
        using var requisicao = CriarRequisicao(CorpoValido(), token, "chave-expirada");

        var resposta = await _cliente.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Registrar_AssinaturaAdulterada_EhRejeitado()
    {
        using var chaveDiferente = RSA.Create(2048);
        var token = TokenDeTeste.Gerar("comerciante-1", chaveDeAssinatura: chaveDiferente);
        using var requisicao = CriarRequisicao(CorpoValido(), token, "chave-assinatura-invalida");

        var resposta = await _cliente.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Registrar_IdentificadorDeComercianteNoCorpo_EhIgnoradoEAtribuidoAoDaCredencial()
    {
        var token = TokenDeTeste.Gerar("comerciante-a");
        var corpoComComercianteAlheio = new
        {
            tipo = "credito",
            valor = 50m,
            competencia = "2026-08-02",
            descricao = "venda",
            comercianteId = "comerciante-b",
        };
        using var requisicao = CriarRequisicao(corpoComComercianteAlheio, token, "chave-comerciante-ignorado");

        var resposta = await _cliente.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var corpoDaResposta = await resposta.Content.ReadFromJsonAsync<RegistrarLancamentoRespostaDto>();
        var lancamento = _fabrica.Armazenamento.Lancamentos[corpoDaResposta!.LancamentoId];
        Assert.Equal("comerciante-a", lancamento.ComercianteId.Valor);
    }

    [Fact]
    public async Task Registrar_MesmaChavePorComerciantesDistintos_NaoVazaDadoEntreComerciantes()
    {
        const string chaveCompartilhada = "chave-compartilhada-borda";
        var tokenA = TokenDeTeste.Gerar("comerciante-a-borda");
        var tokenB = TokenDeTeste.Gerar("comerciante-b-borda");

        using var requisicaoDeA = CriarRequisicao(CorpoValido(), tokenA, chaveCompartilhada);
        var respostaDeA = await _cliente.SendAsync(requisicaoDeA);
        var corpoDeA = await respostaDeA.Content.ReadFromJsonAsync<RegistrarLancamentoRespostaDto>();

        using var requisicaoDeB = CriarRequisicao(CorpoValido(), tokenB, chaveCompartilhada);
        var respostaDeB = await _cliente.SendAsync(requisicaoDeB);
        var corpoDeB = await respostaDeB.Content.ReadFromJsonAsync<RegistrarLancamentoRespostaDto>();

        Assert.Equal(HttpStatusCode.Created, respostaDeA.StatusCode);
        Assert.Equal(HttpStatusCode.Created, respostaDeB.StatusCode);
        Assert.True(corpoDeA!.Criado);
        Assert.True(corpoDeB!.Criado);
        Assert.NotEqual(corpoDeA.LancamentoId, corpoDeB.LancamentoId);
    }

    [Fact]
    public async Task Registrar_PrimeiraVezDepoisReenviado_DistingueCriacaoDeReenvio()
    {
        var token = TokenDeTeste.Gerar("comerciante-criacao-reenvio");
        const string chave = "chave-criacao-reenvio";

        using var primeiraRequisicao = CriarRequisicao(CorpoValido(), token, chave);
        var primeiraResposta = await _cliente.SendAsync(primeiraRequisicao);
        var primeiroCorpo = await primeiraResposta.Content.ReadFromJsonAsync<RegistrarLancamentoRespostaDto>();

        using var segundaRequisicao = CriarRequisicao(CorpoValido(), token, chave);
        var segundaResposta = await _cliente.SendAsync(segundaRequisicao);
        var segundoCorpo = await segundaResposta.Content.ReadFromJsonAsync<RegistrarLancamentoRespostaDto>();

        Assert.Equal(HttpStatusCode.Created, primeiraResposta.StatusCode);
        Assert.True(primeiroCorpo!.Criado);

        Assert.Equal(HttpStatusCode.OK, segundaResposta.StatusCode);
        Assert.False(segundoCorpo!.Criado);
        Assert.Equal(primeiroCorpo.LancamentoId, segundoCorpo.LancamentoId);
        Assert.Equal(primeiroCorpo.RecebidoEm, segundoCorpo.RecebidoEm);
    }

    [Fact]
    public async Task Registrar_FluxoAutenticado_NenhumLogContemACredencialApresentada()
    {
        var token = TokenDeTeste.Gerar("comerciante-log");
        using var requisicao = CriarRequisicao(CorpoValido(), token, "chave-log");

        await _cliente.SendAsync(requisicao);

        Assert.DoesNotContain(_fabrica.CapturadorDeLog.Mensagens, mensagem => mensagem.Contains(token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Registrar_SemChaveDeIdempotencia_EhRejeitado()
    {
        var token = TokenDeTeste.Gerar("comerciante-sem-chave");
        using var requisicao = CriarRequisicao(CorpoValido(), token, chave: null);

        var resposta = await _cliente.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Registrar_ChaveComSessentaECincoCaracteres_EhRejeitada()
    {
        var token = TokenDeTeste.Gerar("comerciante-chave-longa");
        using var requisicao = CriarRequisicao(CorpoValido(), token, new string('a', 65));

        var resposta = await _cliente.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Registrar_ChaveComCaractereNaoImprimivel_EhRejeitada()
    {
        var token = TokenDeTeste.Gerar("comerciante-chave-invalida");
        using var requisicao = CriarRequisicao(CorpoValido(), token, "chaveinvalida");

        var resposta = await _cliente.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Registrar_ValorZero_RespostaIdentificaARegraViolada()
    {
        var token = TokenDeTeste.Gerar("comerciante-erro-padronizado");
        var corpoInvalido = new { tipo = "credito", valor = 0m, competencia = "2026-08-02", descricao = "venda" };
        using var requisicao = CriarRequisicao(corpoInvalido, token, "chave-erro-padronizado");

        var resposta = await _cliente.SendAsync(requisicao);
        var problema = await resposta.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Equal("ValorNaoPositivo", problema!.Title);
        Assert.DoesNotContain("Exception", problema.Detail ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Registrar_DuasChavesDistintasComMesmoConteudo_ProduzDoisLancamentos()
    {
        var token = TokenDeTeste.Gerar("comerciante-conteudo-identico");

        using var primeiraRequisicao = CriarRequisicao(CorpoValido(), token, "chave-conteudo-identico-1");
        var primeiraResposta = await _cliente.SendAsync(primeiraRequisicao);
        var primeiroCorpo = await primeiraResposta.Content.ReadFromJsonAsync<RegistrarLancamentoRespostaDto>();

        using var segundaRequisicao = CriarRequisicao(CorpoValido(), token, "chave-conteudo-identico-2");
        var segundaResposta = await _cliente.SendAsync(segundaRequisicao);
        var segundoCorpo = await segundaResposta.Content.ReadFromJsonAsync<RegistrarLancamentoRespostaDto>();

        Assert.Equal(HttpStatusCode.Created, primeiraResposta.StatusCode);
        Assert.Equal(HttpStatusCode.Created, segundaResposta.StatusCode);
        Assert.NotEqual(primeiroCorpo!.LancamentoId, segundoCorpo!.LancamentoId);
    }

    [Fact]
    public async Task Registrar_DebitoDeCompensacaoAposCredito_EhTratadoComoLancamentoIndependente()
    {
        var token = TokenDeTeste.Gerar("comerciante-compensacao");
        var credito = new { tipo = "credito", valor = 80m, competencia = "2026-08-02", descricao = "venda equivocada" };
        var debito = new { tipo = "debito", valor = 80m, competencia = "2026-08-02", descricao = "compensação da venda equivocada" };

        using var requisicaoDeCredito = CriarRequisicao(credito, token, "chave-compensacao-credito");
        var respostaDeCredito = await _cliente.SendAsync(requisicaoDeCredito);
        var corpoDeCredito = await respostaDeCredito.Content.ReadFromJsonAsync<RegistrarLancamentoRespostaDto>();

        using var requisicaoDeDebito = CriarRequisicao(debito, token, "chave-compensacao-debito");
        var respostaDeDebito = await _cliente.SendAsync(requisicaoDeDebito);
        var corpoDeDebito = await respostaDeDebito.Content.ReadFromJsonAsync<RegistrarLancamentoRespostaDto>();

        Assert.Equal(HttpStatusCode.Created, respostaDeCredito.StatusCode);
        Assert.Equal(HttpStatusCode.Created, respostaDeDebito.StatusCode);
        Assert.NotEqual(corpoDeCredito!.LancamentoId, corpoDeDebito!.LancamentoId);

        var lancamentoDeCredito = _fabrica.Armazenamento.Lancamentos[corpoDeCredito.LancamentoId];
        Assert.Equal(FluxoCaixa.Lancamentos.Dominio.TipoLancamento.Credito, lancamentoDeCredito.Tipo);
    }
}
