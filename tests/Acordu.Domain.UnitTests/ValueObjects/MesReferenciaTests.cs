using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.ValueObjects;

public sealed class MesReferenciaTests
{
    [Fact]
    public void De_Data_PegaAnoEMes()
    {
        MesReferencia.De(new DateOnly(2026, 9, 30)).Should().Be(new MesReferencia(2026, 9));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Criar_MesInvalido_LancaExcecao(int mes)
    {
        var acao = () => new MesReferencia(2026, mes);

        acao.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Contem_SoDatasDoMes()
    {
        var setembro = new MesReferencia(2026, 9);

        setembro.Contem(new DateOnly(2026, 9, 1)).Should().BeTrue();
        setembro.Contem(new DateOnly(2026, 9, 30)).Should().BeTrue();
        setembro.Contem(new DateOnly(2026, 10, 1)).Should().BeFalse();
        setembro.Contem(new DateOnly(2025, 9, 15)).Should().BeFalse();
    }

    [Fact]
    public void PrimeiroEUltimoDia_ConsideramAnoBissexto()
    {
        var fevereiro = new MesReferencia(2028, 2);

        fevereiro.PrimeiroDia.Should().Be(new DateOnly(2028, 2, 1));
        fevereiro.UltimoDia.Should().Be(new DateOnly(2028, 2, 29));
    }

    [Fact]
    public void Comparacao_OrdenaPorAnoEMes()
    {
        (new MesReferencia(2026, 12) < new MesReferencia(2027, 1)).Should().BeTrue();
        (new MesReferencia(2026, 9) >= new MesReferencia(2026, 9)).Should().BeTrue();
    }

    [Fact]
    public void ToString_FormatoDaApi()
    {
        new MesReferencia(2026, 9).ToString().Should().Be("2026-09");
    }
}
