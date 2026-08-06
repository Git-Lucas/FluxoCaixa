namespace FluxoCaixa.Lancamentos.Infraestrutura.Publicacao;

public sealed class OpcoesRabbitMq
{
    public const string SecaoDeConfiguracao = "RabbitMq";

    public required string ConnectionString { get; init; }
}
