using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.ValueObjects;

public sealed class PoliticaAnexosTests
{
    [Fact]
    public void Permite_IgnoraMaiusculasEEspacos()
    {
        var politica = new PoliticaAnexos(1024, [" Image/PNG ", "application/pdf"], 5);

        politica.Permite("image/png").Should().BeTrue();
        politica.Permite("APPLICATION/PDF").Should().BeTrue();
        politica.Permite("application/zip").Should().BeFalse();
    }

    [Fact]
    public void SemTipos_LancaExcecao()
    {
        var acao = () => new PoliticaAnexos(1024, ["  "], 5);

        acao.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1024, 0)]
    public void LimitesNaoPositivos_LancaExcecao(long tamanho, int maximo)
    {
        var acao = () => new PoliticaAnexos(tamanho, ["image/png"], maximo);

        acao.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Igualdade_TiposEmOutraOrdem_SaoIguais()
    {
        new PoliticaAnexos(10, ["a/b", "c/d"], 2).Should().Be(new PoliticaAnexos(10, ["c/d", "a/b"], 2));
    }
}
