using FluxoCaixa.Consolidado.Testes.Infraestrutura;
using FluxoCaixa.Consolidado.Testes.Persistencia;
using FluxoCaixa.Contratos;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FluxoCaixa.Consolidado.Testes.Consumo;

[Collection(nameof(RabbitMqCollection))]
public class ConsumoRabbitMqTestes(RabbitMqFixture fixture)
{
    private static readonly DateOnly s_competencia = new(2026, 8, 2);

    private readonly RabbitMqFixture _fixture = fixture;

    [Fact]
    public async Task Consumir_EventoValidoPublicadoNaFilaReal_AtualizaOConsolidado()
    {
        var comerciante = $"comerciante-{Guid.NewGuid()}";
        var evento = new EventoLancamentoRegistrado(Guid.NewGuid(), comerciante, "credito", 42m, s_competencia, DateTimeOffset.UtcNow);

        await _fixture.PublicarAsync(evento);

        // Tentativas maiores que o padrão: contêineres RabbitMQ/Postgres concorrentes de outras
        // coleções de teste disputam CPU/IO, e a entrega real pela fila é mais lenta que o
        // transporte em memória usado nos demais testes de consumo.
        var consolidado = await Aguardar.AteAsync(
            async () =>
            {
                await using var escopo = _fixture.CriarEscopo(comerciante);
                return await escopo.DbContext.ConsolidadosDiarios.SingleOrDefaultAsync(c => c.Competencia == s_competencia);
            },
            tentativas: 300,
            intervaloEmMs: 200);

        Assert.NotNull(consolidado);
        Assert.Equal(42m, consolidado.TotalCredito);
    }

    [Fact]
    public async Task Consumir_FalhaPersistente_EsgotaRetentativasESegregaParaFilaDeErro()
    {
        var comerciante = $"comerciante-{Guid.NewGuid()}";
        // Tipo desconhecido faz o consumidor lançar sempre — falha persistente, não transitória.
        var evento = new EventoLancamentoRegistrado(Guid.NewGuid(), comerciante, "tipo-invalido", 10m, s_competencia, DateTimeOffset.UtcNow);

        await _fixture.PublicarAsync(evento);

        var segregado = false;
        for (var tentativa = 0; tentativa < 300 && !segregado; tentativa++)
        {
            segregado = _fixture.CapturadorDeErros.Contem(evento.LancamentoId);
            if (!segregado)
            {
                await Task.Delay(200);
            }
        }

        Assert.True(segregado);

        await using var escopo = _fixture.CriarEscopo(comerciante);
        Assert.False(await escopo.DbContext.ConsolidadosDiarios.AnyAsync(c => c.Competencia == s_competencia));
    }
}
