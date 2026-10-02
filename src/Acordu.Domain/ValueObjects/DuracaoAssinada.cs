namespace Acordu.Domain.ValueObjects;

/// <summary>
/// Minutos inteiros que podem ser negativos. Usado só onde o sinal tem sentido de negócio:
/// ajuste de saldo do banco de horas (recarga positiva, estorno negativo) e saldos calculados.
/// </summary>
public readonly record struct DuracaoAssinada(int Minutos) : IComparable<DuracaoAssinada>
{
    public static DuracaoAssinada Zero => default;

    public bool EhNegativa => Minutos < 0;

    public static implicit operator DuracaoAssinada(Duracao duracao) => new(duracao.Minutos);

    public static DuracaoAssinada FromDuracao(Duracao duracao) => new(duracao.Minutos);

    public static DuracaoAssinada operator +(DuracaoAssinada a, DuracaoAssinada b) => new(checked(a.Minutos + b.Minutos));

    public static DuracaoAssinada operator -(DuracaoAssinada a, DuracaoAssinada b) => new(checked(a.Minutos - b.Minutos));

    public static DuracaoAssinada operator -(DuracaoAssinada a) => new(checked(-a.Minutos));

    public static bool operator <(DuracaoAssinada a, DuracaoAssinada b) => a.Minutos < b.Minutos;

    public static bool operator >(DuracaoAssinada a, DuracaoAssinada b) => a.Minutos > b.Minutos;

    public static bool operator <=(DuracaoAssinada a, DuracaoAssinada b) => a.Minutos <= b.Minutos;

    public static bool operator >=(DuracaoAssinada a, DuracaoAssinada b) => a.Minutos >= b.Minutos;

    public static DuracaoAssinada Add(DuracaoAssinada a, DuracaoAssinada b) => a + b;

    public static DuracaoAssinada Subtract(DuracaoAssinada a, DuracaoAssinada b) => a - b;

    public static DuracaoAssinada Negate(DuracaoAssinada a) => -a;

    public int CompareTo(DuracaoAssinada other) => Minutos.CompareTo(other.Minutos);

    public override string ToString() => $"{Minutos}min";
}
