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

        servicos.AddHealthChecks().AddDbContextCheck<LancamentosDbContext>(tags: ["ready"]);

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
            massTransit.AddEntityFrameworkOutbox<LancamentosDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();

                outbox.DisableInboxCleanupService();
            });

            massTransit.UsingRabbitMq((_, rabbitMq) => rabbitMq.Host(new Uri(opcoesRabbitMq.ConnectionString)));
        });

        servicos.AddOptions<OpcoesDeExpurgo>()
            .Bind(configuracao.GetSection(OpcoesDeExpurgo.SecaoDeConfiguracao));
        servicos.AddHostedService<ExpurgoDeIdempotenciaEmSegundoPlano>();

        servicos.AddSingleton<MedidorDeVolumePendente>();

        return servicos;
    }
}
