using Acordu.Domain.Common;

namespace Acordu.Domain.UnitTests.Common;

/// <summary>Design do F15, D2 (F15-01): o código do endereço de entrada, aleatório, curto e sem caracteres ambíguos.</summary>
public sealed class CodigoDeEntradaTests
{
    [Fact]
    public void Sortear_Varias_OitoCaracteresDoAlfabetoSemRepetir()
    {
        var codigos = Enumerable.Range(0, 1000).Select(_ => CodigoDeEntrada.Sortear()).ToList();

        codigos.Should().OnlyContain(c => c.Length == CodigoDeEntrada.Tamanho && CodigoDeEntrada.Valido(c));
        codigos.Should().OnlyHaveUniqueItems("~39 bits: mil sorteios não repetem");
        string.Concat(codigos).Should().NotContainAny("0", "1", "i", "l", "o");
    }

    [Theory]
    [InlineData("k7m2qdxz", true)]
    [InlineData("23456789", true)]
    [InlineData("k7m2qdx", false)]
    [InlineData("k7m2qdxzz", false)]
    [InlineData("K7M2QDXZ", false)]
    [InlineData("k7m2qd0z", false)]
    [InlineData("k7m2qdlz", false)]
    [InlineData("k7m2qd-z", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Valido_Codigo_SoComTamanhoEAlfabeto(string? codigo, bool esperado)
    {
        CodigoDeEntrada.Valido(codigo).Should().Be(esperado);
    }
}
