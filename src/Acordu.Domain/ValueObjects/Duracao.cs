namespace Acordu.Domain.ValueObjects;

/// <summary>
/// Quantidade de tempo em minutos inteiros, nunca negativa (ADR 0004).
/// O tempo é o "dinheiro" do Acordu: toda conta de horas passa por aqui,
/// e a conversão para "1h30" só acontece na apresentação.
/// </summary>
public readonly record struct Duracao : IComparable<Duracao>
{
    public Duracao(int minutos)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minutos);
        Minutos = minutos;
    }

    public int Minutos { get; }

    public static Duracao Zero => default;

    public static Duracao FromMinutos(int minutos) => new(minutos);

    public static Duracao FromHoras(int horas, int minutos = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(horas);
        ArgumentOutOfRangeException.ThrowIfNegative(minutos);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minutos, 59);
        return new(checked((horas * 60) + minutos));
    }

    public bool EhZero => Minutos == 0;

    public static Duracao operator +(Duracao a, Duracao b) => new(checked(a.Minutos + b.Minutos));

    /// <summary>Subtrai; o resultado não pode ser negativo (use <see cref="DuracaoAssinada"/> para saldos).</summary>
    public static Duracao operator -(Duracao a, Duracao b) => new(a.Minutos - b.Minutos);

    public static bool operator <(Duracao a, Duracao b) => a.Minutos < b.Minutos;

    public static bool operator >(Duracao a, Duracao b) => a.Minutos > b.Minutos;

    public static bool operator <=(Duracao a, Duracao b) => a.Minutos <= b.Minutos;

    public static bool operator >=(Duracao a, Duracao b) => a.Minutos >= b.Minutos;

    public static Duracao Add(Duracao a, Duracao b) => a + b;

    public static Duracao Subtract(Duracao a, Duracao b) => a - b;

    public int CompareTo(Duracao other) => Minutos.CompareTo(other.Minutos);

    public override string ToString() => $"{Minutos}min";
}
