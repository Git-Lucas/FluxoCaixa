namespace FluxoCaixa.Lancamentos.Dominio;

public sealed class Lancamento
{
    private Lancamento(
        Guid id,
        ComercianteId comercianteId,
        TipoLancamento tipo,
        Dinheiro valor,
        DataCompetencia competencia,
        Descricao descricao,
        DateTimeOffset recebidoEm)
    {
        Id = id;
        ComercianteId = comercianteId;
        Tipo = tipo;
        Valor = valor;
        Competencia = competencia;
        Descricao = descricao;
        RecebidoEm = recebidoEm;
    }

    public Guid Id { get; }

    public ComercianteId ComercianteId { get; }

    public TipoLancamento Tipo { get; }

    public Dinheiro Valor { get; }

    public DataCompetencia Competencia { get; }

    public Descricao Descricao { get; }

    public DateTimeOffset RecebidoEm { get; }

    public static Lancamento Registrar(
        string comercianteId,
        string tipo,
        decimal valor,
        DateOnly competencia,
        DateOnly dataCorrente,
        string descricao,
        DateTimeOffset recebidoEm)
        => new(
            Guid.CreateVersion7(),
            new ComercianteId(comercianteId),
            TipoLancamentoExtensoes.Interpretar(tipo),
            new Dinheiro(valor),
            DataCompetencia.Criar(competencia, dataCorrente),
            new Descricao(descricao),
            recebidoEm);

    public static Lancamento Reconstituir(
        Guid id,
        ComercianteId comercianteId,
        TipoLancamento tipo,
        Dinheiro valor,
        DataCompetencia competencia,
        Descricao descricao,
        DateTimeOffset recebidoEm)
        => new(id, comercianteId, tipo, valor, competencia, descricao, recebidoEm);
}
