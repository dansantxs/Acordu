using Acordu.Domain.Entities;

namespace Acordu.Domain.Services;

/// <summary>
/// Cronômetro esquecido (F12, decisão D5): passou 30 min do fim do expediente do dia em que começou, ou 10 h rodando, o
/// que vier primeiro. Sem expediente no dia do início (fim de semana, feriado) ou começado depois do fim, vale só as 10 h.
/// A sugestão de duração ao parar vai do início ao fim do expediente, só quando esse fim já passou: nunca aponta tempo
/// que ainda não correu. Pura, como a <see cref="CalculadoraSla"/>: recebe o instante, não lê o relógio.
/// </summary>
public static class CronometroEsquecido
{
    public static readonly TimeSpan DepoisDoExpediente = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan Maximo = TimeSpan.FromHours(10);

    public static (bool Esquecido, int? SugestaoMinutos) Avaliar(
        DateTimeOffset iniciadoEm, DateTimeOffset agora, CalendarioUtil calendario)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        var fim = FimDoExpediente(iniciadoEm, calendario);
        var limite = iniciadoEm + Maximo;
        if (fim is { } f && f + DepoisDoExpediente < limite)
            limite = f + DepoisDoExpediente;
        int? sugestao = fim is { } fimDoDia && fimDoDia <= agora
            ? Math.Max((int)Math.Floor((fimDoDia - iniciadoEm).TotalMinutes), 1)
            : null;
        return (agora >= limite, sugestao);
    }

    /// <summary>Fim do expediente do dia do início, no fuso do tenant, se vier depois do início.</summary>
    private static DateTimeOffset? FimDoExpediente(DateTimeOffset iniciadoEm, CalendarioUtil calendario)
    {
        var local = TimeZoneInfo.ConvertTime(iniciadoEm, calendario.Fuso);
        var dia = DateOnly.FromDateTime(local.DateTime);
        if (calendario.JanelaDe(dia) is not { } janela)
            return null;
        var fim = CalculadoraSla.ParaUtc(dia.ToDateTime(janela.Fim), calendario.Fuso);
        return fim > iniciadoEm ? fim : null;
    }
}
