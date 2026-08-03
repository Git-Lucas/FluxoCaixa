using FluxoCaixa.Lancamentos.Aplicacao.Portas;
using FluxoCaixa.Lancamentos.Dominio;
using FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Testes;

[Collection(nameof(PublicacaoCollection))]
public class PublicacaoRabbitMqTestes(PublicacaoFixture fixture)
{
    private static readonly DateOnly s_dataCorrente = new(2026, 8, 2);

    private readonly PublicacaoFixture _fixture = fixture;

    [Fact]
    public async Task Registrar_LancamentoValido_PublicaComOsCamposCorretosESemADescricao()
    {
        var comerciante = new ComercianteId($"comerciante-{Guid.NewGuid()}");

        var lancamento = await RegistrarAsync(_fixture.Servicos, comerciante, "chave-conteudo");

        var evento = await AguardarEventoAsync(_fixture.Capturador, lancamento.Id);

        Assert.NotNull(evento);
        Assert.Equal(lancamento.Id, evento.LancamentoId);
        Assert.Equal(comerciante.Valor, evento.ComercianteId);
        Assert.Equal("credito", evento.Tipo);
        Assert.Equal(150.25m, evento.Valor);
        Assert.Equal(s_dataCorrente, evento.Competencia);
    }

    [Fact]
    public async Task Registrar_ComTransporteIndisponivel_ConfirmaMesmoAssimEDespachaAoRestabelecer()
    {
        // Porta de host fixa: o Testcontainers reatribui uma porta nova a cada Stop/Start do mesmo
        // container, o que não reflete a realidade — em produção o host:porta do broker é estável
        // entre reinícios. Fixá-la é o que torna o cenário realista de testar.
        const ushort portaFixaDoHost = 45673;
        const string nomeDaFila = "fluxocaixa.lancamentos.registrados.testes.reconexao";

        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await using var rabbitMq = new RabbitMqBuilder("rabbitmq:4-alpine")
            .WithPortBinding(portaFixaDoHost, 5672)
            .Build();
        await Task.WhenAll(postgres.StartAsync(), rabbitMq.StartAsync());

        using var host = ConstrutorDeHostDeTeste.Construir(postgres.GetConnectionString(), rabbitMq.GetConnectionString(), nomeDaFila);

        await using (var escopoDeMigracao = host.Services.CreateAsyncScope())
        {
            await escopoDeMigracao.ServiceProvider.GetRequiredService<LancamentosDbContext>().Database.MigrateAsync();
        }

        await host.StartAsync();

        var comerciante = new ComercianteId($"comerciante-{Guid.NewGuid()}");

        await rabbitMq.StopAsync();

        // O registro precisa ser confirmado mesmo com o transporte fora do ar — a publicação fica
        // pendente no outbox, não bloqueia o caminho de registro.
        var lancamento = await RegistrarAsync(host.Services, comerciante, "chave-transporte-indisponivel");

        await rabbitMq.StartAsync();

        // Retomada automática: nenhuma intervenção manual, só esperar o serviço de entrega do
        // outbox notar que o transporte voltou. A reconexão do canal com o broker pode demorar mais
        // que a janela padrão de espera, daí o número maior de tentativas aqui.
        var capturador = host.Services.GetRequiredService<CapturadorDeEventos>();
        var evento = await AguardarEventoAsync(capturador, lancamento.Id, tentativas: 60);

        Assert.NotNull(evento);

        await host.StopAsync();
    }

    private static async Task<Lancamento> RegistrarAsync(IServiceProvider servicos, ComercianteId comercianteId, string chave)
    {
        await using var escopo = servicos.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<IDefinidorDeComerciante>().Definir(comercianteId);

        var dbContext = escopo.ServiceProvider.GetRequiredService<LancamentosDbContext>();
        var publishEndpoint = escopo.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var unidadeDeTrabalho = new UnidadeDeTrabalhoEfCore(dbContext, publishEndpoint);
        var repositorio = new LancamentoRepositorio(dbContext);
        var repositorioDeIdempotencia = new RegistroIdempotenciaRepositorio(dbContext);

        var lancamento = Lancamento.Registrar(
            comercianteId.Valor,
            "credito",
            150.25m,
            s_dataCorrente,
            s_dataCorrente,
            "venda de balcão",
            DateTimeOffset.UtcNow);

        repositorio.Adicionar(lancamento);
        repositorioDeIdempotencia.Adicionar(new Aplicacao.RegistroIdempotencia(
            comercianteId, chave, lancamento.Id, $"impressao-{chave}", lancamento.RecebidoEm));

        await unidadeDeTrabalho.SalvarAsync(CancellationToken.None);

        return lancamento;
    }

    private static async Task<Publicacao.EventoLancamentoRegistrado?> AguardarEventoAsync(CapturadorDeEventos capturador, Guid lancamentoId, int tentativas = 30)
    {
        for (var tentativa = 0; tentativa < tentativas; tentativa++)
        {
            var evento = capturador.Encontrar(lancamentoId);
            if (evento is not null)
            {
                return evento;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }

        return null;
    }
}
