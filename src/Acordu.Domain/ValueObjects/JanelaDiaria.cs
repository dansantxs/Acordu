namespace Acordu.Domain.ValueObjects;

/// <summary>Expediente de um dia da semana, em hora local do tenant.</summary>
public sealed record JanelaDiaria
{
    public JanelaDiaria(DayOfWeek dia, TimeOnly inicio, TimeOnly fim)
    {
        if (!Enum.IsDefined(dia))
            throw new ArgumentOutOfRangeException(nameof(dia), dia, "Dia da semana inválido.");
        if (fim <= inicio)
            throw new ArgumentException("O fim do expediente deve ser posterior ao início.", nameof(fim));

        Dia = dia;
        Inicio = inicio;
        Fim = fim;
    }

    public DayOfWeek Dia { get; }

    public TimeOnly Inicio { get; }

    public TimeOnly Fim { get; }

    public Duracao Duracao => Duracao.FromMinutos((int)(Fim - Inicio).TotalMinutes);
}
