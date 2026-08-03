using FluxoCaixa.Lancamentos.Dominio;

namespace FluxoCaixa.Lancamentos.Aplicacao.Portas;

public interface IRegistroIdempotencia
{
    Task<RegistroIdempotencia?> ObterAsync(ComercianteId comercianteId, string chave, CancellationToken cancellationToken);

    void Adicionar(RegistroIdempotencia registro);
}
