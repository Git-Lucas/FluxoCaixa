using FluxoCaixa.Consolidado.Api.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FluxoCaixa.Consolidado.Api.Expurgo;

/// <summary>
/// Expurga, fora do caminho de consumo, os registros de deduplicação com mais de 7 dias — a chave
/// primária de <c>lancamento_processado</c> está no caminho crítico de escrita do consumo e
/// degradaria sem esse expurgo. Ignora o filtro global de comerciante deliberadamente: é limpeza
/// entre comerciantes, não uma operação em nome de um deles.
/// </summary>
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
