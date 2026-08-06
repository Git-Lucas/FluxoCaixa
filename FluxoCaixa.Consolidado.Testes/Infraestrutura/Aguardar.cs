namespace FluxoCaixa.Consolidado.Testes.Infraestrutura;

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
