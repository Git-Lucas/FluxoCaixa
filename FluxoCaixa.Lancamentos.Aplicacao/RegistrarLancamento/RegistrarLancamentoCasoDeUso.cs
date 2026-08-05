using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluxoCaixa.Lancamentos.Aplicacao.Portas;
using FluxoCaixa.Lancamentos.Dominio;

namespace FluxoCaixa.Lancamentos.Aplicacao.RegistrarLancamento;

public sealed class RegistrarLancamentoCasoDeUso(
    ILancamentoRepositorio lancamentoRepositorio,
    IRegistroIdempotencia registroIdempotencia,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    IRelogio relogio,
    IContextoComerciante contextoComerciante)
{
    private readonly ILancamentoRepositorio _lancamentoRepositorio = lancamentoRepositorio;
    private readonly IRegistroIdempotencia _registroIdempotencia = registroIdempotencia;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho = unidadeDeTrabalho;
    private readonly IRelogio _relogio = relogio;
    private readonly IContextoComerciante _contextoComerciante = contextoComerciante;

    public async Task<RegistrarLancamentoResposta> ExecutarAsync(
        RegistrarLancamentoRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        var comercianteId = _contextoComerciante.ComercianteId;
        var impressaoDoConteudo = CalcularImpressaoDoConteudo(requisicao);

        var registroExistente = await _registroIdempotencia.ObterAsync(comercianteId, requisicao.ChaveIdempotencia, cancellationToken);

        if (registroExistente is not null)
        {
            return await ConstruirRespostaDeReenvioAsync(registroExistente, impressaoDoConteudo, cancellationToken);
        }

        var lancamento = Lancamento.Registrar(
            comercianteId.Valor,
            requisicao.Tipo,
            requisicao.Valor,
            requisicao.Competencia,
            _relogio.DataCorrenteEmSaoPaulo,
            requisicao.Descricao,
            _relogio.AgoraUtc);

        _lancamentoRepositorio.Adicionar(lancamento);
        _registroIdempotencia.Adicionar(new RegistroIdempotencia(
            comercianteId,
            requisicao.ChaveIdempotencia,
            lancamento.Id,
            impressaoDoConteudo,
            lancamento.RecebidoEm));

        try
        {
            await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        }
        catch (ConflitoDeIdempotenciaException)
        {
            var registroConcorrente = await _registroIdempotencia
                .ObterAsync(comercianteId, requisicao.ChaveIdempotencia, cancellationToken)
                ?? throw new InvalidOperationException(
                    "Violação de unicidade da chave de idempotência sem registro correspondente.");

            return await ConstruirRespostaDeReenvioAsync(registroConcorrente, impressaoDoConteudo, cancellationToken);
        }

        return new RegistrarLancamentoResposta(lancamento.Id, lancamento.RecebidoEm, Criado: true);
    }

    private async Task<RegistrarLancamentoResposta> ConstruirRespostaDeReenvioAsync(
        RegistroIdempotencia registro,
        string impressaoDoConteudoAtual,
        CancellationToken cancellationToken)
    {
        if (registro.ImpressaoDoConteudo != impressaoDoConteudoAtual)
        {
            throw new ConflitoDeConteudoIdempotenteException();
        }

        var lancamento = await _lancamentoRepositorio.ObterPorIdAsync(registro.LancamentoId, cancellationToken)
            ?? throw new InvalidOperationException("Registro de idempotência sem lançamento correspondente.");

        return new RegistrarLancamentoResposta(lancamento.Id, lancamento.RecebidoEm, Criado: false);
    }

    private static string CalcularImpressaoDoConteudo(RegistrarLancamentoRequisicao requisicao)
    {
        var tipoNormalizado = requisicao.Tipo.Trim().ToLowerInvariant();
        var valorNormalizado = requisicao.Valor.ToString("F2", CultureInfo.InvariantCulture);
        var competenciaNormalizada = requisicao.Competencia.ToString("O", CultureInfo.InvariantCulture);
        var descricaoNormalizada = requisicao.Descricao.Trim();

        var conteudo = string.Join('|', tipoNormalizado, valorNormalizado, competenciaNormalizada, descricaoNormalizada);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(conteudo));

        return Convert.ToHexString(hash);
    }
}
