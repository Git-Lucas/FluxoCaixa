using FluxoCaixa.Lancamentos.Aplicacao.Portas;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace FluxoCaixa.Lancamentos.Api.Testes.Infraestrutura;

public sealed class ApiTestesFactory : WebApplicationFactory<Program>
{
    public ArmazenamentoDeTestes Armazenamento { get; } = new();

    public CapturadorDeLog CapturadorDeLog { get; } = new();

    public RelogioFixo Relogio { get; } = new(new DateOnly(2026, 8, 2), new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IHostedService>();

            servicos.AddSingleton(Armazenamento);
            servicos.AddScoped<UnidadeDeTrabalhoEmMemoria>();
            servicos.AddScoped<ILancamentoRepositorio>(provedor => provedor.GetRequiredService<UnidadeDeTrabalhoEmMemoria>());
            servicos.AddScoped<IRegistroIdempotencia>(provedor => provedor.GetRequiredService<UnidadeDeTrabalhoEmMemoria>());
            servicos.AddScoped<IUnidadeDeTrabalho>(provedor => provedor.GetRequiredService<UnidadeDeTrabalhoEmMemoria>());

            servicos.RemoveAll<IRelogio>();
            servicos.AddSingleton<IRelogio>(Relogio);

            servicos.AddLogging(construtor => construtor.AddProvider(CapturadorDeLog));

            servicos.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, opcoes =>
            {
                opcoes.Authority = null;
                opcoes.RequireHttpsMetadata = false;
                opcoes.TokenValidationParameters.IssuerSigningKey = new RsaSecurityKey(TokenDeTeste.ChavePublica);
            });
        });
    }
}

public sealed class CapturadorDeLog : ILoggerProvider
{
    private readonly List<string> _mensagens = [];
    private readonly Lock _portao = new();

    public IReadOnlyList<string> Mensagens
    {
        get
        {
            lock (_portao)
            {
                return [.. _mensagens];
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new Logger(this);

    public void Dispose()
    {
    }

    private sealed class Logger(CapturadorDeLog capturador) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var mensagem = formatter(state, exception);
            lock (capturador._portao)
            {
                capturador._mensagens.Add(mensagem);
            }
        }
    }
}
