using FluxoCaixa.Lancamentos.Aplicacao.Portas;

namespace FluxoCaixa.Lancamentos.Aplicacao;

public sealed class RelogioSistema(TimeProvider timeProvider) : IRelogio
{
    private static readonly TimeZoneInfo _fusoSaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private readonly TimeProvider _timeProvider = timeProvider;

    public DateTimeOffset AgoraUtc => _timeProvider.GetUtcNow();

    public DateOnly DataCorrenteEmSaoPaulo
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(AgoraUtc, _fusoSaoPaulo).DateTime);
}
