using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Services;

/// <summary>
/// Datas de ocorrência de uma <see cref="RegraRecorrencia"/>. Puro e determinístico. Dia inexistente no mês (29–31)
/// vira o último dia do mês (RF-REC-04), sem "contaminar" os meses seguintes: dia 31 gera 31/01, 28/02, 31/03.
/// </summary>
public static class GeradorDeDatas
{
    /// <summary>Primeira ocorrência em ou depois de <paramref name="aPartirDe"/> (e da âncora); nulo se já passou do fim.</summary>
    public static DateOnly? Proxima(RegraRecorrencia regra, DateOnly aPartirDe)
    {
        ArgumentNullException.ThrowIfNull(regra);
        var limite = aPartirDe > regra.Inicio ? aPartirDe : regra.Inicio;
        var data = regra.Periodicidade == Periodicidade.Semanal
            ? ProximaSemanal(regra.DiaReferencia, limite)
            : ProximaMensal(regra, limite, MesesPorCiclo(regra.Periodicidade));

        return regra.Fim is { } fim && data > fim ? null : data;
    }

    /// <summary>Ocorrências no intervalo fechado [<paramref name="de"/>, <paramref name="ate"/>], em ordem.</summary>
    public static IEnumerable<DateOnly> Ocorrencias(RegraRecorrencia regra, DateOnly de, DateOnly ate)
    {
        ArgumentNullException.ThrowIfNull(regra);
        for (var data = Proxima(regra, de); data is { } d && d <= ate; data = d == DateOnly.MaxValue ? null : Proxima(regra, d.AddDays(1)))
            yield return d;
    }

    private static DateOnly ProximaSemanal(int diaIso, DateOnly limite)
    {
        var alvo = diaIso % 7; // ISO 7 (domingo) = DayOfWeek.Sunday (0)
        var dias = (alvo - (int)limite.DayOfWeek + 7) % 7;
        return limite.AddDays(dias);
    }

    private static DateOnly ProximaMensal(RegraRecorrencia regra, DateOnly limite, int meses)
    {
        var ancora = IndiceMes(regra.Inicio);
        var desde = IndiceMes(limite) - ancora;
        var ciclo = (desde + meses - 1) / meses; // primeiro ciclo cujo mês não é anterior ao do limite
        var data = NoMes(ancora + (ciclo * meses), regra.DiaReferencia);
        return data >= limite ? data : NoMes(ancora + ((ciclo + 1) * meses), regra.DiaReferencia);
    }

    private static int MesesPorCiclo(Periodicidade periodicidade) => periodicidade switch
    {
        Periodicidade.Mensal => 1,
        Periodicidade.Trimestral => 3,
        Periodicidade.Anual => 12,
        _ => throw new ArgumentOutOfRangeException(nameof(periodicidade), periodicidade, "Periodicidade sem ciclo mensal."),
    };

    private static int IndiceMes(DateOnly data) => (data.Year * 12) + data.Month - 1;

    private static DateOnly NoMes(int indiceMes, int dia)
    {
        var ano = indiceMes / 12;
        var mes = (indiceMes % 12) + 1;
        return new DateOnly(ano, mes, Math.Min(dia, DateTime.DaysInMonth(ano, mes)));
    }
}
