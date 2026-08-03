using System.Text;
using FluxoCaixa.Lancamentos.Api.Autenticacao;
using FluxoCaixa.Lancamentos.Api.Erros;
using FluxoCaixa.Lancamentos.Api.Lancamentos;
using FluxoCaixa.Lancamentos.Api.LimiteDeTaxa;
using FluxoCaixa.Lancamentos.Aplicacao.RegistrarLancamento;
using FluxoCaixa.Lancamentos.Infraestrutura.DependencyInjection;
using FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AdicionarInfraestrutura(builder.Configuration);
builder.Services.AddScoped<RegistrarLancamentoCasoDeUso>();

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

builder.Services.AddExceptionHandler<ExcecaoDeDominioParaProblemDetails>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Sem preparação manual de ambiente: a migração roda na inicialização do serviço. Os testes de
// borda sobem a API com adaptadores de persistência em memória, sem banco real — o ambiente
// "Testing" (definido por ApiTestesFactory) pula esta etapa.
if (!app.Environment.IsEnvironment("Testing"))
{
    await using var escopoDeInicializacao = app.Services.CreateAsyncScope();
    var dbContext = escopoDeInicializacao.ServiceProvider.GetRequiredService<LancamentosDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseExceptionHandler();

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapearEndpointsDeLancamentos();

await app.RunAsync();

/// <summary>Ponto de entrada exposto para o host de testes de borda (<c>WebApplicationFactory</c>).</summary>
public sealed partial class Program
{
    private Program()
    {
    }
}
