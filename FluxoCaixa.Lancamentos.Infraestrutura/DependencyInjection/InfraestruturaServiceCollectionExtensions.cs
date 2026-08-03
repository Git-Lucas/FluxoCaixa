using FluxoCaixa.Lancamentos.Aplicacao;
using FluxoCaixa.Lancamentos.Aplicacao.Portas;
using FluxoCaixa.Lancamentos.Infraestrutura.Expurgo;
using FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;
using FluxoCaixa.Lancamentos.Infraestrutura.Publicacao;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FluxoCaixa.Lancamentos.Infraestrutura.DependencyInjection;

public static class InfraestruturaServiceCollectionExtensions
{
    public static IServiceCollection AdicionarInfraestrutura(this IServiceCollection servicos, IConfiguration configuracao)
    {
        var stringDeConexao = configuracao.GetConnectionString("Lancamentos")
            ?? throw new InvalidOperationException("A connection string 'Lancamentos' não foi configurada.");

        servicos.AddDbContext<LancamentosDbContext>(opcoes => opcoes.UseNpgsql(stringDeConexao));

        servicos.AddScoped<ContextoComerciante>();
        servicos.AddScoped<IContextoComerciante>(provedor => provedor.GetRequiredService<ContextoComerciante>());
        servicos.AddScoped<IDefinidorDeComerciante>(provedor => provedor.GetRequiredService<ContextoComerciante>());

        servicos.AddScoped<ILancamentoRepositorio, LancamentoRepositorio>();
        servicos.AddScoped<IRegistroIdempotencia, RegistroIdempotenciaRepositorio>();
        servicos.AddScoped<IUnidadeDeTrabalho, UnidadeDeTrabalhoEfCore>();

        servicos.AddSingleton(TimeProvider.System);
        servicos.AddSingleton<IRelogio, RelogioSistema>();

        var opcoesRabbitMq = configuracao.GetSection(OpcoesRabbitMq.SecaoDeConfiguracao).Get<OpcoesRabbitMq>()
            ?? throw new InvalidOperationException($"A seção '{OpcoesRabbitMq.SecaoDeConfiguracao}' não foi configurada.");

        servicos.AddMassTransit(massTransit =>
        {
            // Outbox transacional: IPublishEndpoint.Publish, chamado fora de um consumidor, grava
            // nas tabelas de outbox do próprio DbContext em vez de entregar direto ao broker — na
            // mesma transação do lançamento e da chave de idempotência.
            massTransit.AddEntityFrameworkOutbox<LancamentosDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();

                // Lançamentos só publica; não há inbox de consumidor a limpar.
                outbox.DisableInboxCleanupService();
            });

            // Nenhum receive endpoint aqui: Lançamentos só publica, nunca consome. Uma fila
            // vinculada sem consumidor não "espera" com segurança — o MassTransit move a mensagem
            // para uma fila `_skipped` assim que percebe que não há handler. A fila real nasce
            // quando o serviço de consolidado (change futura) declarar seu próprio receive endpoint
            // vinculado a este mesmo tipo de evento. Eventos publicados antes disso existir não são
            // persistidos em nenhuma fila — mitigação aceita em design.md: o consolidado se
            // reconstrói relendo a tabela de lançamentos, a fonte da verdade.
            massTransit.UsingRabbitMq((_, rabbitMq) => rabbitMq.Host(new Uri(opcoesRabbitMq.ConnectionString)));
        });

        servicos.AddOptions<OpcoesDeExpurgo>()
            .Bind(configuracao.GetSection(OpcoesDeExpurgo.SecaoDeConfiguracao));
        servicos.AddHostedService<ExpurgoDeIdempotenciaEmSegundoPlano>();

        return servicos;
    }
}
