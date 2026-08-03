using FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Expurgo;

/// <summary>
/// Expurga, fora do caminho de atendimento da requisição, as chaves de idempotência com mais de 7
/// dias — o índice único está no caminho crítico de escrita e degradaria sem esse expurgo. Ignora o
/// filtro global de comerciante deliberadamente: é limpeza entre comerciantes, não uma consulta em
/// nome de um deles.
/// </summary>
internal sealed class ExpurgoDeIdempotenciaEmSegundoPlano(
    IServiceScopeFactory scopeFactory,
    IOptions<OpcoesDeExpurgo> opcoes,
    ILogger<ExpurgoDeIdempotenciaEmSegundoPlano> logger) : BackgroundService
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
                logger.LogError(excecao, "Falha ao expurgar chaves de idempotência expiradas.");
            }
        }
        while (await temporizador.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ExpurgarAsync(CancellationToken cancellationToken)
    {
        using var escopo = scopeFactory.CreateScope();
        var dbContext = escopo.ServiceProvider.GetRequiredService<LancamentosDbContext>();

        var limite = DateTimeOffset.UtcNow - opcoes.Value.ValidadeDaChave;

        await dbContext.RequisicoesIdempotentes
            .IgnoreQueryFilters()
            .Where(requisicao => requisicao.CriadoEm < limite)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
