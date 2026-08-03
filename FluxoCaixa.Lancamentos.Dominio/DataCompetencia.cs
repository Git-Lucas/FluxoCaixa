using System.Globalization;

namespace FluxoCaixa.Lancamentos.Dominio;

/// <summary>
/// Data de calendário, sem componente de hora, em que o lançamento afeta o saldo. Válida quando
/// contida no intervalo entre 90 dias antes da data corrente e a data corrente, ambos os limites
/// inclusivos — a única regra de retroatividade do sistema.
/// </summary>
public readonly record struct DataCompetencia
{
    private const int JanelaEmDias = 90;

    private DataCompetencia(DateOnly valor)
    {
        Valor = valor;
    }

    public DateOnly Valor { get; }

    /// <summary>
    /// Valida a competência informada contra a data corrente e constrói o value object. É o único
    /// caminho de criação usado ao registrar um novo lançamento.
    /// </summary>
    public static DataCompetencia Criar(DateOnly competencia, DateOnly dataCorrente)
    {
        if (competencia > dataCorrente)
        {
            throw new LancamentoInvalidoException(
                RegraViolada.CompetenciaFutura,
                "A data de competência não pode ser posterior à data corrente.");
        }

        if (competencia < dataCorrente.AddDays(-JanelaEmDias))
        {
            throw new LancamentoInvalidoException(
                RegraViolada.CompetenciaForaDaJanela,
                $"A data de competência não pode ser anterior a {JanelaEmDias} dias da data corrente.");
        }

        return new DataCompetencia(competencia);
    }

    /// <summary>
    /// Reconstrói o value object a partir de um lançamento já persistido, sem repetir a validação
    /// da janela. Um reenvio identificado por chave de idempotência devolve a resposta original
    /// mesmo que a competência tenha saído da janela entre o registro e o reenvio.
    /// </summary>
    public static DataCompetencia Reconstituir(DateOnly valor) => new(valor);

    public override string ToString() => Valor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
