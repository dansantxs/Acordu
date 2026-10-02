using Acordu.Domain.Common;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.ValueObjects;

public sealed class SolucaoTests
{
    [Fact]
    public void Criar_ApenasComTexto_ProblemaECausaNulos()
    {
        var solucao = new Solucao("Arte refeita com a data correta.");

        solucao.Texto.Should().Be("Arte refeita com a data correta.");
        solucao.Problema.Should().BeNull();
        solucao.Causa.Should().BeNull();
    }

    [Fact]
    public void Criar_ComEspacos_AparaOsTextos()
    {
        var solucao = new Solucao("  texto  ", "  problema ", " causa ");

        solucao.Texto.Should().Be("texto");
        solucao.Problema.Should().Be("problema");
        solucao.Causa.Should().Be("causa");
    }

    [Fact]
    public void Criar_ProblemaSoComEspacos_ViraNulo()
    {
        new Solucao("texto", "   ").Problema.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Criar_SemTexto_LancaExcecao(string? textoInformado)
    {
        var acao = () => new Solucao(textoInformado!);

        acao.Should().Throw<ArgumentException>().WithParameterName("texto");
    }

    [Fact]
    public void Criar_TextoAcimaDoLimite_LancaExcecao()
    {
        var acao = () => new Solucao(new string('x', LimitesTexto.SolucaoTexto + 1));

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Criar_CausaAcimaDoLimite_LancaExcecao()
    {
        var acao = () => new Solucao("texto", causa: new string('x', LimitesTexto.SolucaoProblemaCausa + 1));

        acao.Should().Throw<ArgumentException>().WithParameterName("causa");
    }

    [Fact]
    public void Igualdade_MesmosTextos_SaoIguais()
    {
        new Solucao("t", "p", "c").Should().Be(new Solucao("t", "p", "c"));
    }
}
