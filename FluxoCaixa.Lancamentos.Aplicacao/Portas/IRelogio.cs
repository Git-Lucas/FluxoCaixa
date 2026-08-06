namespace FluxoCaixa.Lancamentos.Aplicacao.Portas;

public interface IRelogio
{
    DateTimeOffset AgoraUtc { get; }

    DateOnly DataCorrenteEmSaoPaulo { get; }
}
