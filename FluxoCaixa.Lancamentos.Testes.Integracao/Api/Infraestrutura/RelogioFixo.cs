using FluxoCaixa.Lancamentos.Aplicacao.Portas;

namespace FluxoCaixa.Lancamentos.Api.Testes.Infraestrutura;

public sealed class RelogioFixo(DateOnly dataCorrente, DateTimeOffset agoraUtc) : IRelogio
{
    public DateTimeOffset AgoraUtc { get; } = agoraUtc;

    public DateOnly DataCorrenteEmSaoPaulo { get; } = dataCorrente;
}
