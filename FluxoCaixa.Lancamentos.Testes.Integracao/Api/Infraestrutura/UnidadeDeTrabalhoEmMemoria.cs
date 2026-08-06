using System.Collections.Concurrent;
using FluxoCaixa.Lancamentos.Aplicacao;
using FluxoCaixa.Lancamentos.Aplicacao.Portas;
using FluxoCaixa.Lancamentos.Dominio;

namespace FluxoCaixa.Lancamentos.Api.Testes.Infraestrutura;

public sealed class ArmazenamentoDeTestes
{
    public ConcurrentDictionary<Guid, Lancamento> Lancamentos { get; } = new();

    public ConcurrentDictionary<(string ComercianteId, string Chave), RegistroIdempotencia> Idempotencia { get; } = new();
}

internal sealed class UnidadeDeTrabalhoEmMemoria(ArmazenamentoDeTestes armazenamento)
    : ILancamentoRepositorio, IRegistroIdempotencia, IUnidadeDeTrabalho
{
    private readonly List<Lancamento> _lancamentosPendentes = [];
    private readonly List<RegistroIdempotencia> _idempotenciaPendente = [];

    public void Adicionar(Lancamento lancamento) => _lancamentosPendentes.Add(lancamento);

    public void Adicionar(RegistroIdempotencia registro) => _idempotenciaPendente.Add(registro);

    public Task<Lancamento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(armazenamento.Lancamentos.GetValueOrDefault(id));

    public Task<RegistroIdempotencia?> ObterAsync(ComercianteId comercianteId, string chave, CancellationToken cancellationToken)
        => Task.FromResult(armazenamento.Idempotencia.GetValueOrDefault((comercianteId.Valor, chave)));

    public Task SalvarAsync(CancellationToken cancellationToken)
    {
        foreach (var registro in _idempotenciaPendente)
        {
            if (!armazenamento.Idempotencia.TryAdd((registro.ComercianteId.Valor, registro.Chave), registro))
            {
                throw new ConflitoDeIdempotenciaException();
            }
        }

        foreach (var lancamento in _lancamentosPendentes)
        {
            armazenamento.Lancamentos[lancamento.Id] = lancamento;
        }

        return Task.CompletedTask;
    }
}
