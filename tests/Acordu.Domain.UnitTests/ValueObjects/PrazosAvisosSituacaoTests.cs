using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.ValueObjects;

public sealed class PrazosAvisosSituacaoTests
{
    [Fact]
    public void Criar_DiasForaDeOrdemERepetidos_OrdenaSemRepetir()
    {
        var prazos = new PrazosAvisosSituacao([7, 1, 7], 7, TimeSpan.FromDays(30));

        prazos.DiasAntesDoFim.Should().Equal(1, 7);
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 7, 0 })]
    [InlineData(new[] { -1 })]
    public void Criar_DiasAntesDoFimInvalidos_LancaExcecao(int[] dias)
    {
        var acao = () => new PrazosAvisosSituacao(dias, 7, TimeSpan.FromDays(30));

        acao.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("diasAntesDoFim");
    }

    [Fact]
    public void Criar_DiasAntesDoExpurgoZero_LancaExcecao()
    {
        var acao = () => new PrazosAvisosSituacao([7, 1], 0, TimeSpan.FromDays(30));

        acao.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("diasAntesDoExpurgo");
    }

    [Fact]
    public void Criar_PrazoAteExpurgoZero_LancaExcecao()
    {
        var acao = () => new PrazosAvisosSituacao([7, 1], 7, TimeSpan.Zero);

        acao.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("prazoAteExpurgo");
    }

    [Fact]
    public void Equals_MesmosValores_Iguais()
    {
        new PrazosAvisosSituacao([1, 7], 7, TimeSpan.FromDays(30))
            .Should().Be(new PrazosAvisosSituacao([7, 1], 7, TimeSpan.FromDays(30)));
    }
}
