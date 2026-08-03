using FluxoCaixa.Lancamentos.Aplicacao.Portas;

namespace FluxoCaixa.Lancamentos.Aplicacao;

/// <summary>
/// Relógio sobre <see cref="TimeProvider"/>, para que a borda dos 90 dias e a virada do dia sejam
/// testáveis sem esperar o relógio real.
/// </summary>
public sealed class RelogioSistema(TimeProvider timeProvider) : IRelogio
{
    private static readonly TimeZoneInfo s_fusoSaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private readonly TimeProvider _timeProvider = timeProvider;

    public DateTimeOffset AgoraUtc => _timeProvider.GetUtcNow();

    public DateOnly DataCorrenteEmSaoPaulo
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(AgoraUtc, s_fusoSaoPaulo).DateTime);
}
