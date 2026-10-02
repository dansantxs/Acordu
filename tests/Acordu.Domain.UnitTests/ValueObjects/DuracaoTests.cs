using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.ValueObjects;

public sealed class DuracaoTests
{
    [Fact]
    public void Construtor_Negativo_LancaExcecao()
    {
        var acao = () => new Duracao(-1);

        acao.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Default_EhZero()
    {
        Duracao duracao = default;

        duracao.Should().Be(Duracao.Zero);
        duracao.EhZero.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 30, 90)]
    [InlineData(8, 0, 480)]
    [InlineData(0, 59, 59)]
    public void FromHoras_ConverteParaMinutos(int horas, int minutos, int esperado)
    {
        Duracao.FromHoras(horas, minutos).Minutos.Should().Be(esperado);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 60)]
    public void FromHoras_ValoresInvalidos_LancaExcecao(int horas, int minutos)
    {
        var acao = () => Duracao.FromHoras(horas, minutos);

        acao.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Soma_SomaMinutos()
    {
        (Duracao.FromMinutos(45) + Duracao.FromMinutos(30)).Should().Be(Duracao.FromMinutos(75));
    }

    [Fact]
    public void Subtracao_ResultadoPositivo_SubtraiMinutos()
    {
        (Duracao.FromMinutos(90) - Duracao.FromMinutos(30)).Should().Be(Duracao.FromMinutos(60));
    }

    [Fact]
    public void Subtracao_ResultadoNegativo_LancaExcecao()
    {
        var acao = () => Duracao.FromMinutos(30) - Duracao.FromMinutos(31);

        acao.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Comparacao_OrdenaPorMinutos()
    {
        var menor = Duracao.FromMinutos(10);
        var maior = Duracao.FromMinutos(20);

        (menor < maior).Should().BeTrue();
        (maior >= menor).Should().BeTrue();
        menor.CompareTo(maior).Should().BeNegative();
    }

    [Fact]
    public void Igualdade_PorValor()
    {
        Duracao.FromHoras(1).Should().Be(Duracao.FromMinutos(60));
    }
}
