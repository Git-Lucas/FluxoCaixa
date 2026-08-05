using FluxoCaixa.Consolidado.Api.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FluxoCaixa.Consolidado.Api.Expurgo;

internal sealed class ExpurgoDeLancamentoProcessadoEmSegundoPlano(
    IServiceScopeFactory scopeFactory,
    IOptions<OpcoesDeExpurgo> opcoes,
    ILogger<ExpurgoDeLancamentoProcessadoEmSegundoPlano> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var temporizador = new PeriodicTimer(opcoes.Value.Intervalo);

        do
        {
            try
            {
                await ExpurgarAsync(stoppingToken);
            }
            catch (Exception excecao) when (excecao is not OperationCanceledException)
            {
                logger.LogError(excecao, "Falha ao expurgar registros de deduplicação expirados.");
            }
        }
        while (await temporizador.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ExpurgarAsync(CancellationToken cancellationToken)
    {
        using var escopo = scopeFactory.CreateScope();
        var dbContext = escopo.ServiceProvider.GetRequiredService<ConsolidadoDbContext>();

        var limite = DateTimeOffset.UtcNow - opcoes.Value.Retencao;

        await dbContext.LancamentosProcessados
            .IgnoreQueryFilters()
            .Where(lancamento => lancamento.ProcessadoEm < limite)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
