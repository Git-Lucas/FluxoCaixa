namespace FluxoCaixa.Consolidado.Testes.Infraestrutura;

/// <summary>Poll com limite de tentativas para aguardar efeitos assíncronos (consumo de mensagem) se propagarem.</summary>
internal static class Aguardar
{
    public static async Task<T?> AteAsync<T>(Func<Task<T?>> consulta, int tentativas = 60, int intervaloEmMs = 100)
        where T : class
    {
        for (var tentativa = 0; tentativa < tentativas; tentativa++)
        {
            var resultado = await consulta();
            if (resultado is not null)
            {
                return resultado;
            }

            await Task.Delay(intervaloEmMs);
        }

        return null;
    }
}
