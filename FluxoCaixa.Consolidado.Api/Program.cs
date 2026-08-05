using FluxoCaixa.Consolidado.Api.Consulta;
using FluxoCaixa.Consolidado.Api.Consumo;
using FluxoCaixa.Consolidado.Api.Expurgo;
using FluxoCaixa.Consolidado.Api.Persistencia;
using FluxoCaixa.Contratos;
using FluxoCaixa.Plataforma.Autenticacao;
using FluxoCaixa.Plataforma.Telemetria;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AdicionarTelemetria("FluxoCaixa.Consolidado");

var stringDeConexao = builder.Configuration.GetConnectionString("Consolidado")
    ?? throw new InvalidOperationException("A connection string 'Consolidado' não foi configurada.");

builder.Services.AddDbContext<ConsolidadoDbContext>(opcoes => opcoes.UseNpgsql(stringDeConexao));

builder.Services.AddHealthChecks().AddDbContextCheck<ConsolidadoDbContext>(tags: ["ready"]);

builder.Services.AddScoped<ContextoComerciante>();
builder.Services.AddScoped<IContextoComerciante>(provedor => provedor.GetRequiredService<ContextoComerciante>());
builder.Services.AddScoped<IDefinidorDeComerciante>(provedor => provedor.GetRequiredService<ContextoComerciante>());

builder.Services.AddOptions<OpcoesDeExpurgo>()
    .Bind(builder.Configuration.GetSection(OpcoesDeExpurgo.SecaoDeConfiguracao));
builder.Services.AddHostedService<ExpurgoDeLancamentoProcessadoEmSegundoPlano>();

var opcoesRabbitMq = builder.Configuration.GetSection(OpcoesRabbitMq.SecaoDeConfiguracao).Get<OpcoesRabbitMq>()
    ?? throw new InvalidOperationException($"A seção '{OpcoesRabbitMq.SecaoDeConfiguracao}' não foi configurada.");

builder.Services.AddMassTransit(massTransit =>
{
    massTransit.AddConsumer<ConsumidorDeEventoLancamentoRegistrado>();
    massTransit.AddConsumer<ConsumidorDeFalhaPersistente>();

    massTransit.UsingRabbitMq((contexto, rabbitMq) =>
    {
        rabbitMq.Host(new Uri(opcoesRabbitMq.ConnectionString));

        rabbitMq.ReceiveEndpoint("fluxocaixa-consolidado", endpoint =>
        {
            endpoint.UseMessageRetry(retry => retry.Intervals(
                TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)));

            endpoint.ConfigureConsumer<ConsumidorDeEventoLancamentoRegistrado>(contexto);
        });

        rabbitMq.ReceiveEndpoint("fluxocaixa-consolidado-falhas", endpoint =>
        {
            endpoint.ConfigureConsumer<ConsumidorDeFalhaPersistente>(contexto);
        });
    });
});

var opcoesAutenticacao = builder.AdicionarAutenticacaoDeComerciante();

builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    await using var escopoDeInicializacao = app.Services.CreateAsyncScope();
    var dbContext = escopoDeInicializacao.ServiceProvider.GetRequiredService<ConsolidadoDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseExceptionHandler();

app.UseAuthentication();
app.UsarIdentificacaoDoComercianteNoLog(opcoesAutenticacao.ClaimDoComerciante);
app.UseAuthorization();

app.MapearEndpointsDeConsulta();

app.MapHealthChecks("/health/healthy", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapOpenApi();
app.UseSwaggerUI(opcoes => opcoes.SwaggerEndpoint("/openapi/v1.json", "FluxoCaixa.Consolidado"));

await app.RunAsync();

public sealed partial class Program
{
    private Program()
    {
    }
}
