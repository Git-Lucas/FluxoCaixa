using System.Data;
using System.Diagnostics.Metrics;
using FluxoCaixa.Lancamentos.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Publicacao;

public sealed class MedidorDeVolumePendente
{
    private static readonly Meter _meter = new("FluxoCaixa.Lancamentos");

    private readonly IServiceScopeFactory _fabricaDeEscopos;

    public MedidorDeVolumePendente(IServiceScopeFactory fabricaDeEscopos)
    {
        _fabricaDeEscopos = fabricaDeEscopos;

        _meter.CreateObservableGauge(
            "fluxocaixa.lancamentos.publicacao_pendente",
            ContarPendentes,
            unit: "{lancamento}",
            description: "Lançamentos registrados cuja publicação ainda não foi despachada.");
    }

    private IEnumerable<Measurement<long>> ContarPendentes()
    {
        using var escopo = _fabricaDeEscopos.CreateScope();
        var dbContext = escopo.ServiceProvider.GetRequiredService<LancamentosDbContext>();
        var conexao = dbContext.Database.GetDbConnection();

        if (conexao.State != ConnectionState.Open)
        {
            conexao.Open();
        }

        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM \"OutboxMessage\"";
        var contagem = (long)comando.ExecuteScalar()!;

        return [new Measurement<long>(contagem)];
    }
}
