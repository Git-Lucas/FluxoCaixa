using FluxoCaixa.Lancamentos.Api.Erros;
using FluxoCaixa.Lancamentos.Api.Lancamentos;
using FluxoCaixa.Lancamentos.Aplicacao.RegistrarLancamento;
using FluxoCaixa.Lancamentos.Infraestrutura.DependencyInjection;
using FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;
using FluxoCaixa.Lancamentos.Infraestrutura.Publicacao;
using FluxoCaixa.Plataforma.Autenticacao;
using FluxoCaixa.Plataforma.Telemetria;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AdicionarTelemetria("FluxoCaixa.Lancamentos");

builder.Services.AdicionarInfraestrutura(builder.Configuration);
builder.Services.AddScoped<RegistrarLancamentoCasoDeUso>();

var opcoesAutenticacao = builder.AdicionarAutenticacaoDeComerciante();

builder.Services.AddExceptionHandler<ExcecaoDeDominioParaProblemDetails>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    await using var escopoDeInicializacao = app.Services.CreateAsyncScope();
    var dbContext = escopoDeInicializacao.ServiceProvider.GetRequiredService<LancamentosDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseExceptionHandler();

app.UseAuthentication();
app.UsarIdentificacaoDoComercianteNoLog(opcoesAutenticacao.ClaimDoComerciante);
app.UseAuthorization();

app.MapearEndpointsDeLancamentos();

app.MapHealthChecks("/health/healthy", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapOpenApi();
app.UseSwaggerUI(opcoes => opcoes.SwaggerEndpoint("/openapi/v1.json", "FluxoCaixa.Lancamentos"));

app.Services.GetRequiredService<MedidorDeVolumePendente>();

await app.RunAsync();

public sealed partial class Program
{
    private Program()
    {
    }
}
