namespace Acordu.Domain.ValueObjects;

/// <summary>Intervalo de datas civis, inclusivo nas pontas; sem fim = em aberto.</summary>
public sealed record Periodo
{
    public Periodo(DateOnly inicio, DateOnly? fim = null)
    {
        if (fim is { } f && f < inicio)
            throw new ArgumentException("O fim do período não pode ser anterior ao início.", nameof(fim));

        Inicio = inicio;
        Fim = fim;
    }

    public DateOnly Inicio { get; }

    public DateOnly? Fim { get; }

    public bool EmAberto => Fim is null;

    public bool Contem(DateOnly data) => data >= Inicio && (Fim is null || data <= Fim);
}
