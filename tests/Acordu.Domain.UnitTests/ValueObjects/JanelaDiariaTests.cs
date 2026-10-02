using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.ValueObjects;

public sealed class JanelaDiariaTests
{
    [Fact]
    public void Construtor_FimAntesOuIgualAoInicio_LancaExcecao()
    {
        var acao = () => new JanelaDiaria(DayOfWeek.Monday, new TimeOnly(18, 0), new TimeOnly(18, 0));

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Construtor_DiaInvalido_LancaExcecao()
    {
        var acao = () => new JanelaDiaria((DayOfWeek)9, new TimeOnly(8, 0), new TimeOnly(18, 0));

        acao.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Duracao_EhOTamanhoDaJanela()
    {
        var janela = new JanelaDiaria(DayOfWeek.Friday, new TimeOnly(8, 0), new TimeOnly(17, 30));

        janela.Duracao.Should().Be(Duracao.FromHoras(9, 30));
    }
}
