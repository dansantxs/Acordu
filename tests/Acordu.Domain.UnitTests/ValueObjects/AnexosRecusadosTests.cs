using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.ValueObjects;

/// <summary>Design do F15, D6 e D14 (F15-15): a lista dos anexos recusados do registro de entrada.</summary>
public sealed class AnexosRecusadosTests
{
    [Fact]
    public void Construtor_Itens_GuardaNaOrdem()
    {
        var recusados = new AnexosRecusados(
        [
            new AnexoRecusado("b.exe", MotivoRecusaAnexo.TipoNaoPermitido),
            new AnexoRecusado("a.mp4", MotivoRecusaAnexo.GrandeDemais),
        ]);

        recusados.Itens.Select(i => i.NomeArquivo).Should().Equal("b.exe", "a.mp4");
    }

    [Fact]
    public void Construtor_SemItens_ArgumentException()
    {
        var criar = () => new AnexosRecusados([]);

        criar.Should().Throw<ArgumentException>().WithParameterName("itens");
    }

    [Fact]
    public void Equals_MesmosItensNaMesmaOrdem_Iguais()
    {
        var a = new AnexosRecusados([new AnexoRecusado("a.exe", MotivoRecusaAnexo.TipoNaoPermitido)]);
        var b = new AnexosRecusados([new AnexoRecusado("a.exe", MotivoRecusaAnexo.TipoNaoPermitido)]);
        var c = new AnexosRecusados([new AnexoRecusado("a.exe", MotivoRecusaAnexo.AlemDoLimite)]);

        a.Should().Be(b);
        a.Should().NotBe(c);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnexoRecusado_SemNome_ArgumentException(string nome)
    {
        var criar = () => new AnexoRecusado(nome, MotivoRecusaAnexo.TipoNaoPermitido);

        criar.Should().Throw<ArgumentException>().WithParameterName("nomeArquivo");
    }

    [Fact]
    public void AnexoRecusado_MotivoForaDoEnum_ArgumentOutOfRange()
    {
        var criar = () => new AnexoRecusado("a.exe", (MotivoRecusaAnexo)99);

        criar.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("motivo");
    }
}
