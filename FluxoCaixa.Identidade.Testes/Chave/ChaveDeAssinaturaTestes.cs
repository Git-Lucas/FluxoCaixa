using FluxoCaixa.Identidade.Api.Chave;
using Xunit;

namespace FluxoCaixa.Identidade.Testes.Chave;

public sealed class ChaveDeAssinaturaTestes : IDisposable
{
    private readonly string _caminho = Path.Combine(Path.GetTempPath(), $"identidade-chave-testes-{Guid.NewGuid():N}.pem");

    public void Dispose()
    {
        if (File.Exists(_caminho))
        {
            File.Delete(_caminho);
        }
    }

    [Fact]
    public void CarregarOuGerar_ArquivoAusente_GeraEGrava()
    {
        Assert.False(File.Exists(_caminho));

        using var chave = ChaveDeAssinatura.CarregarOuGerar(_caminho);

        Assert.True(File.Exists(_caminho));
        Assert.False(string.IsNullOrWhiteSpace(chave.Kid));
    }

    [Fact]
    public void CarregarOuGerar_ArquivoPresente_ReaproveitaAMesmaChave()
    {
        using var primeiraCarga = ChaveDeAssinatura.CarregarOuGerar(_caminho);
        var moduloOriginal = primeiraCarga.Rsa.ExportParameters(false).Modulus;

        using var segundaCarga = ChaveDeAssinatura.CarregarOuGerar(_caminho);
        var moduloReaproveitado = segundaCarga.Rsa.ExportParameters(false).Modulus;

        Assert.Equal(moduloOriginal, moduloReaproveitado);
    }

    [Fact]
    public void CarregarOuGerar_MesmaChave_KidEstavelEntreCargas()
    {
        using var primeiraCarga = ChaveDeAssinatura.CarregarOuGerar(_caminho);
        using var segundaCarga = ChaveDeAssinatura.CarregarOuGerar(_caminho);

        Assert.Equal(primeiraCarga.Kid, segundaCarga.Kid);
    }

    [Fact]
    public void CarregarOuGerar_ChavesDiferentes_KidsDiferentes()
    {
        var outroCaminho = Path.Combine(Path.GetTempPath(), $"identidade-chave-testes-{Guid.NewGuid():N}.pem");
        try
        {
            using var primeiraChave = ChaveDeAssinatura.CarregarOuGerar(_caminho);
            using var segundaChave = ChaveDeAssinatura.CarregarOuGerar(outroCaminho);

            Assert.NotEqual(primeiraChave.Kid, segundaChave.Kid);
        }
        finally
        {
            File.Delete(outroCaminho);
        }
    }
}
