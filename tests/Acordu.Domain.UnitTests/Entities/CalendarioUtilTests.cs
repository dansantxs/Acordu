using Acordu.Domain.Entities;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class CalendarioUtilTests
{
    private static readonly TimeOnly Oito = new(8, 0);
    private static readonly TimeOnly Dezoito = new(18, 0);

    [Fact]
    public void Padrao_SegundaASextaDas8As18EmBrasilia()
    {
        var calendario = Calendarios.Padrao();

        calendario.FusoHorario.Should().Be("America/Sao_Paulo");
        calendario.Expediente.Select(j => j.Dia).Should().BeEquivalentTo<DayOfWeek>(
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);
        calendario.Expediente.Should().OnlyContain(j => j.Inicio == Oito && j.Fim == Dezoito);
        calendario.Feriados.Should().BeEmpty();
    }

    [Fact]
    public void Criar_FusoDesconhecido_LancaCalendarioInvalido()
    {
        var acao = () => CalendarioUtil.Criar(Calendarios.Tenant, "Marte/Olympus", [new JanelaDiaria(DayOfWeek.Monday, Oito, Dezoito)]);

        acao.Should().Throw<CalendarioInvalidoException>();
    }

    [Fact]
    public void Criar_SemDiaUtil_LancaCalendarioInvalido()
    {
        var acao = () => CalendarioUtil.Criar(Calendarios.Tenant, CalendarioUtil.FusoPadrao, []);

        acao.Should().Throw<CalendarioInvalidoException>();
    }

    [Fact]
    public void Criar_DiaRepetido_LancaCalendarioInvalido()
    {
        var acao = () => CalendarioUtil.Criar(
            Calendarios.Tenant,
            CalendarioUtil.FusoPadrao,
            [new JanelaDiaria(DayOfWeek.Monday, Oito, Dezoito), new JanelaDiaria(DayOfWeek.Monday, Oito, new TimeOnly(12, 0))]);

        acao.Should().Throw<CalendarioInvalidoException>();
    }

    [Fact]
    public void DefinirFeriados_IgnoraDuplicatas()
    {
        var calendario = Calendarios.Padrao();

        calendario.DefinirFeriados([Calendarios.FeriadoDoze, Calendarios.FeriadoDoze]);

        calendario.Feriados.Should().ContainSingle();
    }

    [Fact]
    public void JanelaDe_FeriadoOuFimDeSemana_EhNula()
    {
        var calendario = Calendarios.ComFeriadoDoze();

        calendario.JanelaDe(Calendarios.FeriadoDoze).Should().BeNull();
        calendario.JanelaDe(new DateOnly(2026, 10, 10)).Should().BeNull(); // sábado
        calendario.JanelaDe(new DateOnly(2026, 10, 13)).Should().NotBeNull(); // terça
    }

    [Fact]
    public void AlterarFuso_FusoDesconhecido_MantemOAnterior()
    {
        var calendario = Calendarios.Padrao();

        var acao = () => calendario.AlterarFuso("Nada/Lugar");

        acao.Should().Throw<CalendarioInvalidoException>();
        calendario.FusoHorario.Should().Be("America/Sao_Paulo");
    }
}
