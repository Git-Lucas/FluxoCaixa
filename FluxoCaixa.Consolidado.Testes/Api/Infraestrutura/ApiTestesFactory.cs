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
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<DbContextOptions<ConsolidadoDbContext>>();
            servicos.AddDbContext<ConsolidadoDbContext>(opcoes => opcoes.UseNpgsql(_postgres.GetConnectionString()));

            servicos.RemoveAll<IHostedService>();

            servicos.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, opcoes =>
            {
                opcoes.Authority = null;
                opcoes.RequireHttpsMetadata = false;
                opcoes.TokenValidationParameters.IssuerSigningKey = new RsaSecurityKey(TokenDeTeste.ChavePublica);
            });
        });
    }
}
