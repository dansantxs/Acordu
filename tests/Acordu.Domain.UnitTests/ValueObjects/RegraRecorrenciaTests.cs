using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.ValueObjects;

public sealed class RegraRecorrenciaTests
{
    private static readonly DateOnly Inicio = new(2026, 10, 1);

    [Theory]
    [InlineData(Periodicidade.Semanal, 0)]
    [InlineData(Periodicidade.Semanal, 8)]
    [InlineData(Periodicidade.Mensal, 0)]
    [InlineData(Periodicidade.Mensal, 32)]
    [InlineData(Periodicidade.Anual, 32)]
    public void DiaForaDoIntervalo_LancaExcecao(Periodicidade periodicidade, int dia)
    {
        var acao = () => new RegraRecorrencia(periodicidade, dia, 0, Inicio);

        acao.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("diaReferencia");
    }

    [Theory]
    [InlineData(Periodicidade.Semanal, 7)]
    [InlineData(Periodicidade.Trimestral, 31)]
    public void DiaNoLimite_Aceita(Periodicidade periodicidade, int dia)
    {
        var acao = () => new RegraRecorrencia(periodicidade, dia, 0, Inicio);

        acao.Should().NotThrow();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(RegraRecorrencia.AntecedenciaMaximaDias + 1)]
    public void AntecedenciaForaDoIntervalo_LancaExcecao(int antecedencia)
    {
        var acao = () => new RegraRecorrencia(Periodicidade.Mensal, 10, antecedencia, Inicio);

        acao.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FimAntesDoInicio_LancaExcecao()
    {
        var acao = () => new RegraRecorrencia(Periodicidade.Mensal, 10, 0, Inicio, Inicio.AddDays(-1));

        acao.Should().Throw<ArgumentException>().WithParameterName("fim");
    }

    [Fact]
    public void Igualdade_PorValor()
    {
        new RegraRecorrencia(Periodicidade.Mensal, 10, 3, Inicio)
            .Should().Be(new RegraRecorrencia(Periodicidade.Mensal, 10, 3, Inicio));
    }
}
