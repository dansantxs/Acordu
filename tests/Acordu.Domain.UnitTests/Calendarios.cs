using Acordu.Domain.Entities;

namespace Acordu.Domain.UnitTests;

/// <summary>Calendários e instantes de apoio aos testes (outubro de 2026, horário de Brasília).</summary>
internal static class Calendarios
{
    public static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>Segunda-feira, 12/10/2026 (Nossa Senhora Aparecida).</summary>
    public static readonly DateOnly FeriadoDoze = new(2026, 10, 12);

    public static CalendarioUtil Padrao() => CalendarioUtil.Padrao(Tenant);

    public static CalendarioUtil ComFeriadoDoze()
    {
        var calendario = CalendarioUtil.Padrao(Tenant);
        calendario.DefinirFeriados([FeriadoDoze]);
        return calendario;
    }

    /// <summary>Instante em hora de Brasília (UTC-3), num dia de outubro de 2026.</summary>
    public static DateTimeOffset Brasilia(int dia, int hora, int minuto = 0) =>
        new(2026, 10, dia, hora, minuto, 0, TimeSpan.FromHours(-3));
}
