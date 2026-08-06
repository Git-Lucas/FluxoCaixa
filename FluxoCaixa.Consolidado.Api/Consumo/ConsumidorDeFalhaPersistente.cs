using FluxoCaixa.Contratos;
using MassTransit;

namespace FluxoCaixa.Consolidado.Api.Consumo;

internal sealed class ConsumidorDeFalhaPersistente : IConsumer<Fault<EventoLancamentoRegistrado>>
{
    public Task Consume(ConsumeContext<Fault<EventoLancamentoRegistrado>> context)
    {
        MetricasDoConsolidado.MensagensSegregadas.Add(1);
        return Task.CompletedTask;
    }
}
