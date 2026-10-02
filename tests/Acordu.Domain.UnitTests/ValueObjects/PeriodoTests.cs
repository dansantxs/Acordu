using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.ValueObjects;

public sealed class PeriodoTests
{
    private static readonly DateOnly Inicio = new(2026, 10, 1);
    private static readonly DateOnly Fim = new(2026, 10, 31);

    [Fact]
    public void Construtor_FimAntesDoInicio_LancaExcecao()
    {
        var acao = () => new Periodo(Fim, Inicio);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Contem_IncluiAsPontas()
    {
        var periodo = new Periodo(Inicio, Fim);

        periodo.Contem(Inicio).Should().BeTrue();
        periodo.Contem(Fim).Should().BeTrue();
        periodo.Contem(Inicio.AddDays(-1)).Should().BeFalse();
        periodo.Contem(Fim.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void Contem_EmAberto_IncluiQualquerDataAPartirDoInicio()
    {
        var periodo = new Periodo(Inicio);

        periodo.EmAberto.Should().BeTrue();
        periodo.Contem(new DateOnly(2030, 1, 1)).Should().BeTrue();
        periodo.Contem(Inicio.AddDays(-1)).Should().BeFalse();
    }

    [Fact]
    public void Construtor_InicioIgualAoFim_EhValido()
    {
        var periodo = new Periodo(Inicio, Inicio);

        periodo.Contem(Inicio).Should().BeTrue();
    }
}
