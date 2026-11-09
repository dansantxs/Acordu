using Acordu.Domain.Entities;
using Acordu.Domain.Services;

namespace Acordu.Domain.UnitTests.Services;

/// <summary>F12-17 (decisão D5): 30 min depois do fim do expediente do dia do início, ou 10 h rodando.</summary>
public sealed class CronometroEsquecidoTests
{
    private static readonly CalendarioUtil Calendario = CalendarioUtil.Padrao(Guid.NewGuid()); // seg–sex, 08–18, Brasília

    // Segunda, 09/11/2026, no horário de Brasília (UTC-3).
    private static DateTimeOffset Segunda(int hora, int minuto = 0) => new(2026, 11, 9, hora, minuto, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void AntesDoFimMais30_NaoEsta_ESugereAteOFim()
    {
        var (esquecido, sugestao) = CronometroEsquecido.Avaliar(Segunda(14), Segunda(18, 29), Calendario);

        esquecido.Should().BeFalse();
        sugestao.Should().Be(240);
        CronometroEsquecido.Avaliar(Segunda(14), Segunda(17), Calendario).SugestaoMinutos.Should().BeNull("o fim ainda não chegou");
    }

    [Fact]
    public void TrintaMinutosDepoisDoFim_Esta()
    {
        CronometroEsquecido.Avaliar(Segunda(14), Segunda(18, 30), Calendario).Esquecido.Should().BeTrue();
    }

    [Fact]
    public void DezHorasAntesDoFim_Esta_ESemSugestaoNoFuturo()
    {
        // Começou às 07:00 (antes do expediente): 10 h é às 17:00, antes das 18:30. O fim (18:00) ainda não chegou:
        // sugerir até ele apontaria uma hora que não correu.
        var (esquecido, sugestao) = CronometroEsquecido.Avaliar(Segunda(7), Segunda(17), Calendario);
        esquecido.Should().BeTrue();
        sugestao.Should().BeNull();
        CronometroEsquecido.Avaliar(Segunda(7), Segunda(16, 59), Calendario).Esquecido.Should().BeFalse();
    }

    [Fact]
    public void FimJaPassou_SugereDoInicioAteOFim()
    {
        CronometroEsquecido.Avaliar(Segunda(7), Segunda(19), Calendario).SugestaoMinutos.Should().Be(660);
    }

    [Fact]
    public void ComecouDepoisDoFim_SoAs10Horas_ESemSugestao()
    {
        var (antes, sugestao) = CronometroEsquecido.Avaliar(Segunda(19), Segunda(23), Calendario);
        antes.Should().BeFalse();
        sugestao.Should().BeNull();
        CronometroEsquecido.Avaliar(Segunda(19), Segunda(19).AddHours(10), Calendario).Esquecido.Should().BeTrue();
    }

    [Fact]
    public void FimDeSemanaOuFeriado_SoAs10Horas()
    {
        var sabado = new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.FromHours(-3));
        CronometroEsquecido.Avaliar(sabado, sabado.AddHours(9), Calendario).Esquecido.Should().BeFalse();
        CronometroEsquecido.Avaliar(sabado, sabado.AddHours(10), Calendario).Esquecido.Should().BeTrue();

        var comFeriado = CalendarioUtil.Padrao(Guid.NewGuid());
        comFeriado.DefinirFeriados([new DateOnly(2026, 11, 9)]);
        var (esquecido, sugestao) = CronometroEsquecido.Avaliar(Segunda(14), Segunda(19), comFeriado);
        esquecido.Should().BeFalse();
        sugestao.Should().BeNull();
    }
}
