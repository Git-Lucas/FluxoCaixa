using System.Text;
using FluxoCaixa.Consolidado.Api.Autenticacao;
using FluxoCaixa.Consolidado.Api.Consulta;
using FluxoCaixa.Consolidado.Api.Consumo;
using FluxoCaixa.Consolidado.Api.Expurgo;
using FluxoCaixa.Consolidado.Api.LimiteDeTaxa;
using FluxoCaixa.Consolidado.Api.Persistencia;
using FluxoCaixa.Contratos;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var stringDeConexao = builder.Configuration.GetConnectionString("Consolidado")
    ?? throw new InvalidOperationException("A connection string 'Consolidado' não foi configurada.");

builder.Services.AddDbContext<ConsolidadoDbContext>(opcoes => opcoes.UseNpgsql(stringDeConexao));

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

    // Consolidado só consome; não há outbox de publicação nem inbox do MassTransit — a
    // deduplicação é feita à mão em lancamento_processado (design.md, decisão 4).
    massTransit.UsingRabbitMq((contexto, rabbitMq) =>
    {
        rabbitMq.Host(new Uri(opcoesRabbitMq.ConnectionString));

        rabbitMq.ReceiveEndpoint("fluxocaixa-consolidado", endpoint =>
        {
            // Um único pipeline de retentativa, em processo, com intervalo crescente — cobre tanto
            // a retentativa imediata (primeiro intervalo zero) quanto o efeito de redelivery
            // (intervalos maiores depois). UseDelayedRedelivery exigiria o plugin de exchange
            // atrasado do RabbitMQ, não presente na imagem do docker-compose; esta é a forma que
            // não depende dele. Esgotadas as tentativas, o MassTransit move a mensagem para a fila
            // `fluxocaixa-consolidado_error` automaticamente (design.md, decisão 10).
            endpoint.UseMessageRetry(retry => retry.Intervals(
                TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)));

            endpoint.ConfigureConsumer<ConsumidorDeEventoLancamentoRegistrado>(contexto);
        });
    });
});

builder.Services
    .AddOptions<OpcoesAutenticacao>()
    .Bind(builder.Configuration.GetSection(OpcoesAutenticacao.SecaoDeConfiguracao))
    .Validate(opcoes => !string.IsNullOrWhiteSpace(opcoes.ChaveDeAssinatura))
    .ValidateOnStart();

var opcoesAutenticacao = builder.Configuration
    .GetSection(OpcoesAutenticacao.SecaoDeConfiguracao)
    .Get<OpcoesAutenticacao>()
    ?? throw new InvalidOperationException("A seção 'Autenticacao' não foi configurada.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opcoesDoJwt =>
    {
        // MapInboundClaims = false preserva o nome curto da claim ("sub"), em vez de remapeá-la
        // para a URI longa de ClaimTypes.
        opcoesDoJwt.MapInboundClaims = false;
        opcoesDoJwt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = opcoesAutenticacao.Emissor,
            ValidateAudience = true,
            ValidAudience = opcoesAutenticacao.Audiencia,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opcoesAutenticacao.ChaveDeAssinatura)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(opcoes => PoliticaDeLimiteDeTaxa.Configurar(opcoes, opcoesAutenticacao.ClaimDoComerciante));

builder.Services.AddProblemDetails();

var app = builder.Build();

// Sem preparação manual de ambiente: a migração roda na inicialização do serviço. O ambiente
// "Testing" (definido pela fábrica de testes de borda) pula esta etapa.
if (!app.Environment.IsEnvironment("Testing"))
{
    await using var escopoDeInicializacao = app.Services.CreateAsyncScope();
    var dbContext = escopoDeInicializacao.ServiceProvider.GetRequiredService<ConsolidadoDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapearEndpointsDeConsulta();

await app.RunAsync();

/// <summary>Ponto de entrada exposto para o host de testes de borda (<c>WebApplicationFactory</c>).</summary>
public sealed partial class Program
{
    private Program()
    {
    }
}
