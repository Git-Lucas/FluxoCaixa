namespace FluxoCaixa.Lancamentos.Dominio;

/// <summary>
/// Um lançamento de crédito ou débito no caixa de um comerciante. Imutável desde o registro: não
/// existe operação de alteração, exclusão nem estorno.
/// </summary>
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

    /// <summary>
    /// Registra um novo lançamento a partir de tipos primitivos: a construção dos value objects, e
    /// portanto a validação das invariantes de domínio, acontece aqui dentro, não na camada de
    /// aplicação. Gera um identificador UUID versão 7, ordenável no tempo, para que a inserção
    /// preserve a localidade do índice em vez de espalhá-la.
    /// </summary>
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

    /// <summary>
    /// Reconstrói um lançamento já persistido, sem repetir validação: o conteúdo já foi validado
    /// no momento do registro original.
    /// </summary>
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
