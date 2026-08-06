using FluxoCaixa.Lancamentos.Aplicacao;
using FluxoCaixa.Lancamentos.Dominio;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Testes;

[Collection(nameof(PostgresCollection))]
public class PersistenciaLancamentosTestes(PostgresFixture fixture)
{
    private static readonly DateOnly _dataCorrente = new(2026, 8, 2);

    private readonly PostgresFixture _fixture = fixture;

    [Fact]
    public async Task Registrar_LancamentoValido_GravaLancamentoOutboxEIdempotenciaNaMesmaTransacao()
    {
        var comerciante = NovoComerciante();

        var lancamentoId = await CriarLancamentoAsync(comerciante, "chave-atomicidade");

        await using var escopo = _fixture.CriarEscopo(comerciante);

        var lancamentoPersistido = await escopo.DbContext.Lancamentos.SingleAsync();
        var idempotenciaPersistida = await escopo.DbContext.RequisicoesIdempotentes.SingleAsync();

        _ = await escopo.DbContext.Set<OutboxMessage>()
            .SingleAsync(mensagem => mensagem.Body.Contains(lancamentoId.ToString()));

        Assert.Equal(lancamentoId, lancamentoPersistido.Id);
        Assert.Equal(lancamentoId, idempotenciaPersistida.LancamentoId);
    }

    [Fact]
    public async Task Registrar_DuasRequisicoesConcorrentesComMesmaChave_ResultaEmUmUnicoLancamentoEUmaUnicaMensagem()
    {
        var comerciante = NovoComerciante();
        const string chave = "chave-concorrencia";
        var portaoDeLargada = new TaskCompletionSource();

        async Task<Guid> RegistrarAsync()
        {
            await using var escopo = _fixture.CriarEscopo(comerciante);

            var lancamento = CriarLancamento(comerciante);
            escopo.RepositorioDeLancamento.Adicionar(lancamento);
            escopo.RepositorioDeIdempotencia.Adicionar(new RegistroIdempotencia(
                comerciante, chave, lancamento.Id, "impressao-concorrencia", lancamento.RecebidoEm));

            await portaoDeLargada.Task;

            try
            {
                await escopo.UnidadeDeTrabalho.SalvarAsync(CancellationToken.None);
                return lancamento.Id;
            }
            catch (ConflitoDeIdempotenciaException)
            {
                return Guid.Empty;
            }
        }

        var tarefaUm = RegistrarAsync();
        var tarefaDois = RegistrarAsync();
        portaoDeLargada.SetResult();
        var resultados = await Task.WhenAll(tarefaUm, tarefaDois);

        var lancamentoVencedor = Assert.Single(resultados, id => id != Guid.Empty);

        await using var escopoDeLeitura = _fixture.CriarEscopo(comerciante);
        Assert.Equal(1, await escopoDeLeitura.DbContext.Lancamentos.CountAsync());

        Assert.Equal(1, await escopoDeLeitura.DbContext.Set<OutboxMessage>()
            .CountAsync(mensagem => mensagem.Body.Contains(lancamentoVencedor.ToString())));
    }

    [Fact]
    public async Task Consultar_EscopadoAUmComerciante_NaoAlcancaDadoDeOutroComerciante()
    {
        var comercianteA = NovoComerciante();
        var comercianteB = NovoComerciante();

        await CriarLancamentoAsync(comercianteA, "chave-a");
        await CriarLancamentoAsync(comercianteB, "chave-b");

        await using var escopoDeA = _fixture.CriarEscopo(comercianteA);
        var lancamentosVisiveisParaA = await escopoDeA.DbContext.Lancamentos.ToListAsync();
        var idempotenciaVisivelParaA = await escopoDeA.DbContext.RequisicoesIdempotentes.ToListAsync();

        Assert.Single(lancamentosVisiveisParaA);
        Assert.All(lancamentosVisiveisParaA, lancamento => Assert.Equal(comercianteA, lancamento.ComercianteId));
        Assert.Single(idempotenciaVisivelParaA);
        Assert.All(idempotenciaVisivelParaA, requisicao => Assert.Equal(comercianteA, requisicao.ComercianteId));
    }

    [Fact]
    public async Task Registrar_MesmaChavePorComerciantesDistintos_ProduzLancamentosIndependentes()
    {
        var comercianteA = NovoComerciante();
        var comercianteB = NovoComerciante();
        const string chaveCompartilhada = "chave-compartilhada";

        var lancamentoIdDeA = await CriarLancamentoAsync(comercianteA, chaveCompartilhada);
        var lancamentoIdDeB = await CriarLancamentoAsync(comercianteB, chaveCompartilhada);

        Assert.NotEqual(lancamentoIdDeA, lancamentoIdDeB);

        await using var escopoDeA = _fixture.CriarEscopo(comercianteA);
        var registroDeA = await escopoDeA.RepositorioDeIdempotencia.ObterAsync(comercianteA, chaveCompartilhada, CancellationToken.None);
        Assert.Equal(lancamentoIdDeA, registroDeA!.LancamentoId);

        await using var escopoDeB = _fixture.CriarEscopo(comercianteB);
        var registroDeB = await escopoDeB.RepositorioDeIdempotencia.ObterAsync(comercianteB, chaveCompartilhada, CancellationToken.None);
        Assert.Equal(lancamentoIdDeB, registroDeB!.LancamentoId);
    }

    private static ComercianteId NovoComerciante() => new($"comerciante-{Guid.NewGuid()}");

    private static Lancamento CriarLancamento(ComercianteId comercianteId)
        => Lancamento.Registrar(
            comercianteId.Valor,
            "credito",
            150m,
            _dataCorrente,
            _dataCorrente,
            "venda de balcão",
            DateTimeOffset.UtcNow);

    private async Task<Guid> CriarLancamentoAsync(ComercianteId comercianteId, string chave)
    {
        await using var escopo = _fixture.CriarEscopo(comercianteId);

        var lancamento = CriarLancamento(comercianteId);
        escopo.RepositorioDeLancamento.Adicionar(lancamento);
        escopo.RepositorioDeIdempotencia.Adicionar(new RegistroIdempotencia(
            comercianteId, chave, lancamento.Id, $"impressao-{chave}", lancamento.RecebidoEm));

        await escopo.UnidadeDeTrabalho.SalvarAsync(CancellationToken.None);

        return lancamento.Id;
    }
}
