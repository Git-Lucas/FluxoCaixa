using System.Globalization;

namespace FluxoCaixa.Lancamentos.Dominio;

public readonly record struct DataCompetencia
{
    private const int _janelaEmDias = 90;

    private DataCompetencia(DateOnly valor)
    {
        Valor = valor;
    }

    public DateOnly Valor { get; }

    public static DataCompetencia Criar(DateOnly competencia, DateOnly dataCorrente)
    {
        if (competencia > dataCorrente)
        {
            throw new LancamentoInvalidoException(
                RegraViolada.CompetenciaFutura,
                "A data de competência não pode ser posterior à data corrente.");
        }

        if (competencia < dataCorrente.AddDays(-_janelaEmDias))
        {
            throw new LancamentoInvalidoException(
                RegraViolada.CompetenciaForaDaJanela,
                $"A data de competência não pode ser anterior a {_janelaEmDias} dias da data corrente.");
        }

        return new DataCompetencia(competencia);
    }

    public static DataCompetencia Reconstituir(DateOnly valor) => new(valor);

    public override string ToString() => Valor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
