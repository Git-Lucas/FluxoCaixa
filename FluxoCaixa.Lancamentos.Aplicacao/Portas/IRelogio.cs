namespace FluxoCaixa.Lancamentos.Aplicacao.Portas;

public interface IRelogio
{
    /// <summary>Instante corrente, para o instante de recebimento do lançamento.</summary>
    DateTimeOffset AgoraUtc { get; }

    /// <summary>Data corrente no fuso `America/Sao_Paulo`, para a janela de competência.</summary>
    DateOnly DataCorrenteEmSaoPaulo { get; }
}
