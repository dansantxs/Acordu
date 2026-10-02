using Acordu.Domain.Enums;

namespace Acordu.Domain.ValueObjects;

/// <summary>
/// Quando a tarefa recorrente acontece (RF-REC-02). <see cref="Inicio"/> é a âncora: nenhuma ocorrência antes dela, e o
/// mês dela define o ciclo TRIMESTRAL (mês do início, +3, +6...) e ANUAL (sempre o mês do início).
/// <see cref="DiaReferencia"/>: 1–7 no SEMANAL (ISO: 1 = segunda, 7 = domingo); 1–31 nos demais, com clamp ao último
/// dia do mês (RF-REC-04).
/// </summary>
public sealed record RegraRecorrencia
{
    public const int AntecedenciaMaximaDias = 365;

    public RegraRecorrencia(
        Periodicidade periodicidade,
        int diaReferencia,
        int antecedenciaDias,
        DateOnly inicio,
        DateOnly? fim = null)
    {
        if (!Enum.IsDefined(periodicidade))
            throw new ArgumentOutOfRangeException(nameof(periodicidade), periodicidade, "Periodicidade inválida.");
        var maximoDia = periodicidade == Periodicidade.Semanal ? 7 : 31;
        if (diaReferencia < 1 || diaReferencia > maximoDia)
            throw new ArgumentOutOfRangeException(nameof(diaReferencia), diaReferencia, $"Dia de referência deve estar entre 1 e {maximoDia}.");
        ArgumentOutOfRangeException.ThrowIfNegative(antecedenciaDias);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(antecedenciaDias, AntecedenciaMaximaDias);
        if (fim is { } f && f < inicio)
            throw new ArgumentException("O fim da recorrência não pode ser anterior ao início.", nameof(fim));

        Periodicidade = periodicidade;
        DiaReferencia = diaReferencia;
        AntecedenciaDias = antecedenciaDias;
        Inicio = inicio;
        Fim = fim;
    }

    public Periodicidade Periodicidade { get; }

    public int DiaReferencia { get; }

    /// <summary>Quantos dias antes da data de referência a tarefa é gerada.</summary>
    public int AntecedenciaDias { get; }

    public DateOnly Inicio { get; }

    public DateOnly? Fim { get; }
}
