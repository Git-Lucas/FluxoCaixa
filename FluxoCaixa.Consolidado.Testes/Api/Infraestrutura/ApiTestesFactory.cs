using FluxoCaixa.Consolidado.Api.Persistencia;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Xunit;

namespace FluxoCaixa.Consolidado.Testes.Api.Infraestrutura;

/// <summary>
/// Sobe a API real contra um Postgres efêmero (Testcontainers) — o endpoint de consulta acessa o
/// <see cref="ConsolidadoDbContext"/> diretamente, sem um adaptador trocável, então os testes de
/// borda precisam de um banco real por trás. A migração roda uma vez, em <see cref="InitializeAsync"/>.
/// </summary>
public sealed class ApiTestesFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var escopo = Services.CreateAsyncScope();
        var dbContext = escopo.ServiceProvider.GetRequiredService<ConsolidadoDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync() => await _postgres.DisposeAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Sinaliza a Program.cs para pular a migração automática no início — este fixture já cuida
        // dela, uma única vez, em InitializeAsync.
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(servicos =>
        {
            // Program.cs já leu a connection string de appsettings.json antes de Build() — uma
            // configuração adicionada aqui chegaria tarde demais para influenciar aquele valor.
            // Trocar o registro de DbContextOptions é o jeito padrão de redirecionar o EF Core num
            // WebApplicationFactory.
            servicos.RemoveAll<DbContextOptions<ConsolidadoDbContext>>();
            servicos.AddDbContext<ConsolidadoDbContext>(opcoes => opcoes.UseNpgsql(_postgres.GetConnectionString()));

            // Sem MassTransit (exigiria RabbitMQ real) nem expurgo em segundo plano: os testes de
            // borda exercitam autenticação, validação e limite de taxa, não mensageria — já coberta
            // pelas coleções de integração narrow.
            servicos.RemoveAll<IHostedService>();

            // Os testes de borda não sobem o emissor real: injeta a chave pública do par RSA
            // efêmero de TokenDeTeste diretamente, no lugar da busca via Authority. Precisa ser
            // Configure, não PostConfigure — cada Configure roda antes de qualquer PostConfigure,
            // e é o PostConfigure interno do JwtBearer que cria o ConfigurationManager real a
            // partir da Authority (e falharia por exigir HTTPS) se visse a Authority original.
            servicos.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, opcoes =>
            {
                opcoes.Authority = null;
                opcoes.RequireHttpsMetadata = false;
                opcoes.TokenValidationParameters.IssuerSigningKey = new RsaSecurityKey(TokenDeTeste.ChavePublica);
            });
        });
    }
}
