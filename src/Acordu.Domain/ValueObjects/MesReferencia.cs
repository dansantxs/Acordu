using System.Globalization;

namespace Acordu.Domain.ValueObjects;

/// <summary>
/// Mês civil de apuração (ex.: 2026-09): período mensal do consumo e unidade de fechamento dos apontamentos.
/// </summary>
public readonly record struct MesReferencia : IComparable<MesReferencia>
{
    public MesReferencia(int ano, int mes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ano, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ano, 9999);
        ArgumentOutOfRangeException.ThrowIfLessThan(mes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(mes, 12);
        Ano = ano;
        Mes = mes;
    }

    public int Ano { get; }

    public int Mes { get; }

    public DateOnly PrimeiroDia => new(Ano, Mes, 1);

    public DateOnly UltimoDia => new(Ano, Mes, DateTime.DaysInMonth(Ano, Mes));

    public static MesReferencia De(DateOnly data) => new(data.Year, data.Month);

    public bool Contem(DateOnly data) => data.Year == Ano && data.Month == Mes;

    public static bool operator <(MesReferencia a, MesReferencia b) => a.CompareTo(b) < 0;

    public static bool operator >(MesReferencia a, MesReferencia b) => a.CompareTo(b) > 0;

    public static bool operator <=(MesReferencia a, MesReferencia b) => a.CompareTo(b) <= 0;

    public static bool operator >=(MesReferencia a, MesReferencia b) => a.CompareTo(b) >= 0;

    public int CompareTo(MesReferencia other) => ((Ano * 12) + Mes).CompareTo((other.Ano * 12) + other.Mes);

    /// <summary>Formato do contrato da API: <c>yyyy-MM</c>.</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Ano:D4}-{Mes:D2}");
}
