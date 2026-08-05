using FluxoCaixa.Identidade.Api.Chave;
using FluxoCaixa.Identidade.Api.Descoberta;
using FluxoCaixa.Identidade.Api.Emissao;
using FluxoCaixa.Plataforma.LimiteDeTaxa;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddRateLimiter(opcoes => PoliticaDeLimiteDeTaxa.Configurar(
    opcoes,
    claimDoComerciante: "sub",
    autenticado: new LimitesDeTaxa(Rajada: 20, TaxaPorSegundo: 10),
    porOrigem: new LimitesDeTaxa(Rajada: 20, TaxaPorSegundo: 10)));

var app = builder.Build();

app.UseRateLimiter();

app.MapearEndpointDeToken();
app.MapearEndpointsDeDescoberta();

await app.RunAsync();

/// <summary>Ponto de entrada exposto para o host de testes de borda (<c>WebApplicationFactory</c>).</summary>
public sealed partial class Program
{
    private Program()
    {
    }
}
