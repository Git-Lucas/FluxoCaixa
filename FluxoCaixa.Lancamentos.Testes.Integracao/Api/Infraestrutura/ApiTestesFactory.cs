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

/// <summary>
/// Sobe a API real com os adaptadores de persistência e publicação trocados por versões em
/// memória — os testes de borda exercitam autenticação, isolamento, limite de taxa, limite de
/// corpo e Problem Details, não a persistência real, já coberta pelos testes de integração narrow.
/// </summary>
public sealed class ApiTestesFactory : WebApplicationFactory<Program>
{
    public ArmazenamentoDeTestes Armazenamento { get; } = new();

    public CapturadorDeLog CapturadorDeLog { get; } = new();

    public RelogioFixo Relogio { get; } = new(new DateOnly(2026, 8, 2), new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Sinaliza a Program.cs para pular a migração automática no início — não há banco real
        // disponível para os testes de borda, que trocam a persistência por versões em memória.
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

/// <summary>Captura toda mensagem de log emitida durante o teste, para verificar ausência de credencial.</summary>
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
