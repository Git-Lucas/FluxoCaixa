using FluxoCaixa.Lancamentos.Aplicacao.Portas;
using FluxoCaixa.Lancamentos.Aplicacao.RegistrarLancamento;
using FluxoCaixa.Lancamentos.Dominio;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace FluxoCaixa.Lancamentos.Aplicacao.Testes;

public class RegistrarLancamentoCasoDeUsoTestes
{
    private static readonly ComercianteId _comerciante = new("comerciante-1");
    private static readonly DateOnly _dataCorrente = new(2026, 8, 2);
    private static readonly DateTimeOffset _agora = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly ILancamentoRepositorio _lancamentoRepositorio = Substitute.For<ILancamentoRepositorio>();
    private readonly IRegistroIdempotencia _registroIdempotencia = Substitute.For<IRegistroIdempotencia>();
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho = Substitute.For<IUnidadeDeTrabalho>();
    private readonly IRelogio _relogio = Substitute.For<IRelogio>();
    private readonly IContextoComerciante _contextoComerciante = Substitute.For<IContextoComerciante>();

    private readonly RegistrarLancamentoCasoDeUso _casoDeUso;

    public RegistrarLancamentoCasoDeUsoTestes()
    {
        _relogio.AgoraUtc.Returns(_agora);
        _relogio.DataCorrenteEmSaoPaulo.Returns(_dataCorrente);
        _contextoComerciante.ComercianteId.Returns(_comerciante);

        _casoDeUso = new RegistrarLancamentoCasoDeUso(
            _lancamentoRepositorio,
            _registroIdempotencia,
            _unidadeDeTrabalho,
            _relogio,
            _contextoComerciante);
    }

    private static RegistrarLancamentoRequisicao RequisicaoValida(string chave = "chave-1")
        => new(chave, "credito", 100m, _dataCorrente, "venda de balcão");

    [Fact]
    public async Task ExecutarAsync_RequisicaoNova_RegistraEConfirmaComoCriado()
    {
        _registroIdempotencia.ObterAsync(_comerciante, "chave-1", Arg.Any<CancellationToken>())
            .Returns((RegistroIdempotencia?)null);

        var resposta = await _casoDeUso.ExecutarAsync(RequisicaoValida(), CancellationToken.None);

        Assert.True(resposta.Criado);
        Assert.Equal(_agora, resposta.RecebidoEm);
        _lancamentoRepositorio.Received(1).Adicionar(Arg.Any<Lancamento>());
        _registroIdempotencia.Received(1).Adicionar(Arg.Any<RegistroIdempotencia>());
        await _unidadeDeTrabalho.Received(1).SalvarAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecutarAsync_ReenvioComMesmoConteudo_DevolveRespostaOriginalSemGravarNovamente()
    {
        var lancamentoOriginal = LancamentoReconstituido(_dataCorrente.AddDays(-1));
        var impressaoOriginal = ImpressaoEsperada(RequisicaoValida());
        var registro = new RegistroIdempotencia(_comerciante, "chave-1", lancamentoOriginal.Id, impressaoOriginal, lancamentoOriginal.RecebidoEm);

        _registroIdempotencia.ObterAsync(_comerciante, "chave-1", Arg.Any<CancellationToken>()).Returns(registro);
        _lancamentoRepositorio.ObterPorIdAsync(lancamentoOriginal.Id, Arg.Any<CancellationToken>()).Returns(lancamentoOriginal);

        var resposta = await _casoDeUso.ExecutarAsync(RequisicaoValida(), CancellationToken.None);

        Assert.False(resposta.Criado);
        Assert.Equal(lancamentoOriginal.Id, resposta.LancamentoId);
        Assert.Equal(lancamentoOriginal.RecebidoEm, resposta.RecebidoEm);
        _lancamentoRepositorio.DidNotReceive().Adicionar(Arg.Any<Lancamento>());
        _registroIdempotencia.DidNotReceive().Adicionar(Arg.Any<RegistroIdempotencia>());
        await _unidadeDeTrabalho.DidNotReceive().SalvarAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecutarAsync_ReenvioNaViradaDoDia_NaoReexecutaValidacaoDaJanelaDeCompetencia()
    {
        var competenciaForaDaJanelaAtual = _dataCorrente.AddDays(-200);
        var requisicaoOriginal = new RegistrarLancamentoRequisicao("chave-1", "credito", 100m, competenciaForaDaJanelaAtual, "venda de balcão");
        var lancamentoOriginal = LancamentoReconstituido(competenciaForaDaJanelaAtual);
        var registro = new RegistroIdempotencia(_comerciante, "chave-1", lancamentoOriginal.Id, ImpressaoEsperada(requisicaoOriginal), lancamentoOriginal.RecebidoEm);

        _registroIdempotencia.ObterAsync(_comerciante, "chave-1", Arg.Any<CancellationToken>()).Returns(registro);
        _lancamentoRepositorio.ObterPorIdAsync(lancamentoOriginal.Id, Arg.Any<CancellationToken>()).Returns(lancamentoOriginal);
        _relogio.DataCorrenteEmSaoPaulo.Returns(_ => throw new InvalidOperationException(
            "O relógio não deveria ser consultado para revalidar a janela de competência num reenvio."));

        var resposta = await _casoDeUso.ExecutarAsync(requisicaoOriginal, CancellationToken.None);

        Assert.False(resposta.Criado);
        Assert.Equal(lancamentoOriginal.Id, resposta.LancamentoId);
    }

    [Fact]
    public async Task ExecutarAsync_ReenvioComConteudoDiferente_LancaConflitoDeConteudo()
    {
        var lancamentoOriginal = LancamentoReconstituido(_dataCorrente.AddDays(-1));
        var registro = new RegistroIdempotencia(_comerciante, "chave-1", lancamentoOriginal.Id, "impressao-diferente", lancamentoOriginal.RecebidoEm);

        _registroIdempotencia.ObterAsync(_comerciante, "chave-1", Arg.Any<CancellationToken>()).Returns(registro);

        await Assert.ThrowsAsync<ConflitoDeConteudoIdempotenteException>(
            () => _casoDeUso.ExecutarAsync(RequisicaoValida(), CancellationToken.None));

        _lancamentoRepositorio.DidNotReceive().Adicionar(Arg.Any<Lancamento>());
    }

    [Fact]
    public async Task ExecutarAsync_ValorZero_LancaExcecaoDeDominioSemConsumirAChave()
    {
        _registroIdempotencia.ObterAsync(_comerciante, "chave-1", Arg.Any<CancellationToken>())
            .Returns((RegistroIdempotencia?)null);
        var requisicaoInvalida = new RegistrarLancamentoRequisicao("chave-1", "credito", 0m, _dataCorrente, "venda de balcão");

        var excecao = await Assert.ThrowsAsync<LancamentoInvalidoException>(
            () => _casoDeUso.ExecutarAsync(requisicaoInvalida, CancellationToken.None));

        Assert.Equal(RegraViolada.ValorNaoPositivo, excecao.Regra);
        _lancamentoRepositorio.DidNotReceive().Adicionar(Arg.Any<Lancamento>());
        _registroIdempotencia.DidNotReceive().Adicionar(Arg.Any<RegistroIdempotencia>());
        await _unidadeDeTrabalho.DidNotReceive().SalvarAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecutarAsync_ConflitoDeUnicidadeNaGravacao_RelePassaADevolverRespostaDeReenvio()
    {
        var lancamentoConcorrente = LancamentoReconstituido(_dataCorrente);
        var registroConcorrente = new RegistroIdempotencia(
            _comerciante, "chave-1", lancamentoConcorrente.Id, ImpressaoEsperada(RequisicaoValida()), lancamentoConcorrente.RecebidoEm);

        _registroIdempotencia.ObterAsync(_comerciante, "chave-1", Arg.Any<CancellationToken>())
            .Returns((RegistroIdempotencia?)null, registroConcorrente);

        _unidadeDeTrabalho.SalvarAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ConflitoDeIdempotenciaException());

        _lancamentoRepositorio.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(lancamentoConcorrente);

        var resposta = await _casoDeUso.ExecutarAsync(RequisicaoValida(), CancellationToken.None);

        Assert.False(resposta.Criado);
        Assert.Equal(lancamentoConcorrente.Id, resposta.LancamentoId);
    }

    private static Lancamento LancamentoReconstituido(DateOnly competencia)
        => Lancamento.Reconstituir(
            Guid.CreateVersion7(),
            _comerciante,
            TipoLancamento.Credito,
            new Dinheiro(100m),
            DataCompetencia.Reconstituir(competencia),
            new Descricao("venda de balcão"),
            _agora.AddDays(-1));

    private static string ImpressaoEsperada(RegistrarLancamentoRequisicao requisicao)
    {
        var tipoNormalizado = requisicao.Tipo.Trim().ToLowerInvariant();
        var valorNormalizado = requisicao.Valor.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        var competenciaNormalizada = requisicao.Competencia.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        var descricaoNormalizada = requisicao.Descricao.Trim();

        var conteudo = string.Join('|', tipoNormalizado, valorNormalizado, competenciaNormalizada, descricaoNormalizada);
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(conteudo));

        return Convert.ToHexString(hash);
    }
}
