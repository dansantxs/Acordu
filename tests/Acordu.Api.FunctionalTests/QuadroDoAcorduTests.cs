using Acordu.Api.Operacao;

namespace Acordu.Api.FunctionalTests;

/// <summary>Spec do F13, F13-05: o quadro da impressão digital, estável entre versões.</summary>
public sealed class QuadroDoAcorduTests
{
    private static async Task LancarAsync()
    {
        await Task.Yield();
        throw new InvalidOperationException("x");
    }

    [Fact]
    public async Task De_ErroNumMetodoAsync_TipoEMetodoReais()
    {
        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(LancarAsync);

        QuadroDoAcordu.De(excecao).Should().Be("Acordu.Api.FunctionalTests.QuadroDoAcorduTests.LancarAsync");
    }

    [Fact]
    public void De_ErroNumaLambda_MetodoQueADeclara()
    {
        Action lancar = () => throw new InvalidOperationException("x");

        var excecao = Assert.Throws<InvalidOperationException>(lancar);

        QuadroDoAcordu.De(excecao).Should().Be("Acordu.Api.FunctionalTests.QuadroDoAcorduTests.De_ErroNumaLambda_MetodoQueADeclara");
    }

    [Fact]
    public void De_ExcecaoNuncaLancada_Nulo() =>
        QuadroDoAcordu.De(new InvalidOperationException("sem pilha")).Should().BeNull();
}
