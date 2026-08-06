namespace FluxoCaixa.Consolidado.Api.Consumo;

public sealed class OpcoesRabbitMq
{
    public const string SecaoDeConfiguracao = "RabbitMq";

    public required string ConnectionString { get; init; }
}
