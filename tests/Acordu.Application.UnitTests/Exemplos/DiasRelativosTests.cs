using Acordu.Application.Exemplos;

namespace Acordu.Application.UnitTests.Exemplos;

/// <summary>
/// As datas relativas dos dados fictícios (design do F14, D13): a mesma conta do bot do manual, para a API e o bot
/// chegarem às mesmas datas a partir do mesmo JSON.
/// </summary>
public sealed class DiasRelativosTests
{
    private static readonly DiasRelativos Terca = new(new DateOnly(2026, 10, 6));
    private static readonly DiasRelativos Sabado = new(new DateOnly(2026, 10, 10));

    [Theory]
    [InlineData(0, 2026, 10, 6)]
    [InlineData(1, 2026, 10, 7)]
    [InlineData(4, 2026, 10, 12)]
    [InlineData(-1, 2026, 10, 5)]
    [InlineData(-2, 2026, 10, 2)]
    [InlineData(-6, 2026, 9, 28)]
    public void Util_DiasUteis_PulaOFimDeSemana(int dias, int ano, int mes, int dia)
    {
        Terca.Util(dias).Should().Be(new DateOnly(ano, mes, dia));
    }

    [Fact]
    public void Util_ZeroNoSabado_EOProprioSabado()
    {
        Sabado.Util(0).Should().Be(new DateOnly(2026, 10, 10));
        Sabado.Util(1).Should().Be(new DateOnly(2026, 10, 12));
        Sabado.Util(-1).Should().Be(new DateOnly(2026, 10, 9));
    }

    [Fact]
    public void UltimoUtil_NoFimDeSemana_ASextaAnterior()
    {
        Sabado.UltimoUtil.Should().Be(new DateOnly(2026, 10, 9));
        Terca.UltimoUtil.Should().Be(new DateOnly(2026, 10, 6));
    }

    [Theory]
    [InlineData(0, 2026, 10, 1)]
    [InlineData(-3, 2026, 7, 1)]
    public void InicioDoMes_Meses_PrimeiroDiaDoMes(int meses, int ano, int mes, int dia)
    {
        Terca.InicioDoMes(meses).Should().Be(new DateOnly(ano, mes, dia));
    }

    [Fact]
    public void DoApontamento_DiaZero_OUltimoDiaUtil()
    {
        Sabado.DoApontamento(Apontamento(0, null)).Should().Be(new DateOnly(2026, 10, 9));
    }

    [Fact]
    public void DoApontamento_SemanaAntesDoUltimoUtil_Entra()
    {
        Terca.DoApontamento(Apontamento(-1, ApontamentoFicticio.Semana)).Should().Be(new DateOnly(2026, 10, 5));
    }

    [Theory]
    [InlineData(-2)] // sexta da semana anterior
    [InlineData(-4)]
    public void DoApontamento_SemanaForaDaSemanaDoUltimoUtil_FicaDeFora(int dia)
    {
        Terca.DoApontamento(Apontamento(dia, ApontamentoFicticio.Semana)).Should().BeNull();
    }

    [Fact]
    public void DoApontamento_SemanaNoSabado_NaoRepeteOUltimoUtil()
    {
        // Sábado: o -1 é a sexta, que já é o último dia útil.
        Sabado.DoApontamento(Apontamento(-1, ApontamentoFicticio.Semana)).Should().BeNull();
        Sabado.DoApontamento(Apontamento(-2, ApontamentoFicticio.Semana)).Should().Be(new DateOnly(2026, 10, 8));
    }

    [Theory]
    [InlineData(-2, 2026, 10, 2)]
    [InlineData(-6, 2026, 10, 6)] // 28/09, antes do mês: vai para o último dia útil
    public void DoApontamento_Mes_AntesDoMesVaiParaOUltimoUtil(int dia, int ano, int mes, int diaEsperado)
    {
        Terca.DoApontamento(Apontamento(dia, ApontamentoFicticio.Mes)).Should().Be(new DateOnly(ano, mes, diaEsperado));
    }

    private static ApontamentoFicticio Apontamento(int dia, string? limite) => new("marina", "conta", dia, 30, "Atendimento", limite);
}
