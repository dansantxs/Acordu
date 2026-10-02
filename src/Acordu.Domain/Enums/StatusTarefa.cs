namespace Acordu.Domain.Enums;

/// <summary>A_FAZER → FAZENDO → CONCLUIDA; A_FAZER/FAZENDO → CANCELADA (final); CONCLUIDA → FAZENDO (reabrir).</summary>
public enum StatusTarefa
{
    AFazer = 1,
    Fazendo = 2,
    Concluida = 3,
    Cancelada = 4,
}
