using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FluxoCaixa.Identidade.Testes.Api.Infraestrutura;

public sealed class IdentidadeApiTestesFactory : WebApplicationFactory<Program>
{
    public const string ClientIdValido = "comerciante-testes";
    public const string ClientSecretValido = "segredo-super-secreto-de-teste";

    private readonly string _caminhoDaChave = Path.Combine(Path.GetTempPath(), $"identidade-api-testes-{Guid.NewGuid():N}.pem");

    public CapturadorDeLog CapturadorDeLog { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuracao) =>
        {
            configuracao.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Chave:CaminhoDoArquivo"] = _caminhoDaChave,
                ["Emissao:Clientes:0:ClientId"] = ClientIdValido,
                ["Emissao:Clientes:0:ClientSecret"] = ClientSecretValido,
            });
        });

        builder.ConfigureServices(servicos => servicos.AddLogging(construtor => construtor.AddProvider(CapturadorDeLog)));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && File.Exists(_caminhoDaChave))
        {
            File.Delete(_caminhoDaChave);
        }
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
