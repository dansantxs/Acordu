using Acordu.Domain.Entities;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Services;

/// <summary>
/// Aritmética de horas úteis (ADR 0003). Pura e determinística: anda pelo expediente na hora local
/// do tenant, pula o que está fora do horário, fins de semana sem expediente e feriados, e devolve
/// instantes em UTC. Nunca lê o relógio.
/// </summary>
public static class CalculadoraSla
{
    /// <summary>Limite de segurança contra calendário que nunca fecha a conta (≈ 10 anos).</summary>
    private const int MaximoDeDias = 3700;

    /// <summary>
    /// Instante em que se completam <paramref name="horasUteis"/> a partir de <paramref name="inicio"/>.
    /// Início fora do expediente começa a contar na próxima abertura. Duração zero devolve o próprio início.
    /// </summary>
    public static DateTimeOffset Somar(CalendarioUtil calendario, DateTimeOffset inicio, Duracao horasUteis)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        if (horasUteis.EhZero)
            return inicio.ToUniversalTime();

        var fuso = calendario.Fuso;
        var cursor = TimeZoneInfo.ConvertTime(inicio, fuso).DateTime;
        var restante = TimeSpan.FromMinutes(horasUteis.Minutos);

        for (var dias = 0; dias < MaximoDeDias; dias++)
        {
            var data = DateOnly.FromDateTime(cursor);
            if (calendario.JanelaDe(data) is { } janela)
            {
                var abertura = data.ToDateTime(janela.Inicio);
                var fechamento = data.ToDateTime(janela.Fim);
                if (cursor < abertura)
                    cursor = abertura;

                if (cursor < fechamento)
                {
                    var disponivel = fechamento - cursor;
                    if (restante <= disponivel)
                        return ParaUtc(cursor + restante, fuso);

                    restante -= disponivel;
                }
            }

            cursor = data.AddDays(1).ToDateTime(TimeOnly.MinValue);
        }

        throw new InvalidOperationException("O calendário não tem horas úteis suficientes para completar o prazo.");
    }

    /// <summary>
    /// Minutos úteis inteiros (arredondados para baixo) entre dois instantes. Zero se <paramref name="ate"/> não é posterior a <paramref name="de"/>.
    /// </summary>
    public static Duracao MinutosUteisEntre(CalendarioUtil calendario, DateTimeOffset de, DateTimeOffset ate)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        if (ate <= de)
            return Duracao.Zero;

        var fuso = calendario.Fuso;
        var inicio = TimeZoneInfo.ConvertTime(de, fuso).DateTime;
        var fim = TimeZoneInfo.ConvertTime(ate, fuso).DateTime;
        var total = TimeSpan.Zero;

        for (var data = DateOnly.FromDateTime(inicio); data <= DateOnly.FromDateTime(fim); data = data.AddDays(1))
        {
            if (calendario.JanelaDe(data) is not { } janela)
                continue;

            var abertura = Max(data.ToDateTime(janela.Inicio), inicio);
            var fechamento = Min(data.ToDateTime(janela.Fim), fim);
            if (fechamento > abertura)
                total += fechamento - abertura;
        }

        return Duracao.FromMinutos((int)total.TotalMinutes);
    }

    /// <summary>Hora local do tenant para UTC, pulando a hora inexistente do horário de verão (também usado pelo <see cref="CronometroEsquecido"/>).</summary>
    internal static DateTimeOffset ParaUtc(DateTime local, TimeZoneInfo fuso)
    {
        // Hora inexistente (início de horário de verão) avança até existir.
        while (fuso.IsInvalidTime(local))
            local = local.AddMinutes(30);

        var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), fuso);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
