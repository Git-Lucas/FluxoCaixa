using FluxoCaixa.Identidade.Api.Chave;
using FluxoCaixa.Identidade.Api.Descoberta;
using FluxoCaixa.Identidade.Api.Emissao;
using FluxoCaixa.Plataforma.Telemetria;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.AdicionarTelemetria("FluxoCaixa.Identidade");

builder.Services
    .AddOptions<OpcoesDoEmissor>()
    .Bind(builder.Configuration.GetSection(OpcoesDoEmissor.SecaoDeConfiguracao))
    .Validate(opcoes => opcoes.Clientes.Count > 0, "Ao menos um cliente deve ser configurado.")
    .Validate(
        opcoes => opcoes.Clientes.All(cliente => !string.IsNullOrWhiteSpace(cliente.ClientId) && !string.IsNullOrWhiteSpace(cliente.ClientSecret)),
        "client_id e client_secret não podem ser vazios.")
    .Validate(
        opcoes => opcoes.Clientes.Select(cliente => cliente.ClientId).Distinct(StringComparer.Ordinal).Count() == opcoes.Clientes.Count,
        "client_id duplicado na semente.")
    .ValidateOnStart();

var opcoesDaChave = builder.Configuration.GetSection(OpcoesDaChave.SecaoDeConfiguracao).Get<OpcoesDaChave>() ?? new OpcoesDaChave();
builder.Services.AddSingleton(ChaveDeAssinatura.CarregarOuGerar(opcoesDaChave.CaminhoDoArquivo));

builder.Services.AddHealthChecks().AddCheck<AptidaoDoEmissorHealthCheck>("chave-de-assinatura", tags: ["ready"]);

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapearEndpointDeToken();
app.MapearEndpointsDeDescoberta();

app.MapHealthChecks("/health/healthy", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapOpenApi();
app.UseSwaggerUI(opcoes => opcoes.SwaggerEndpoint("/openapi/v1.json", "FluxoCaixa.Identidade"));

await app.RunAsync();

public sealed partial class Program
{
    private Program()
    {
    }
}
