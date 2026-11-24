namespace Acordu.Application.Exemplos;

/// <summary>
/// As datas relativas dos dados fictícios (<see cref="DadosFicticios"/>) a partir de "hoje" no fuso da empresa. Dia útil
/// aqui é de segunda a sexta, sem feriados, a mesma conta do bot do manual (<c>apps/web/ajuda/demonstracao.ts</c>): as
/// duas pontas leem o mesmo arquivo e precisam chegar às mesmas datas.
/// </summary>
public sealed class DiasRelativos
{
    public DiasRelativos(DateOnly hoje)
    {
        Hoje = hoje;
    }

    public DateOnly Hoje { get; }

    /// <summary>Hoje, ou a sexta anterior no fim de semana: apontamento não vai para o futuro.</summary>
    public DateOnly UltimoUtil => FimDeSemana(Hoje) ? Util(-1) : Hoje;

    /// <summary>Soma dias úteis (negativo volta), pulando sábado e domingo; 0 é hoje, mesmo no fim de semana.</summary>
    public DateOnly Util(int dias)
    {
        var dia = Hoje;
        var passo = Math.Sign(dias);
        for (var falta = Math.Abs(dias); falta > 0;)
        {
            dia = dia.AddDays(passo);
            if (!FimDeSemana(dia))
                falta--;
        }

        return dia;
    }

    /// <summary>Primeiro dia do mês corrente mais <paramref name="meses"/> (negativo volta).</summary>
    public DateOnly InicioDoMes(int meses) => new DateOnly(Hoje.Year, Hoje.Month, 1).AddMonths(meses);

    /// <summary>
    /// O dia do apontamento (<see cref="ApontamentoFicticio.Dia"/> e <see cref="ApontamentoFicticio.Limite"/>), ou nulo
    /// quando ele fica de fora (fora da semana do último dia útil).
    /// </summary>
    public DateOnly? DoApontamento(ApontamentoFicticio apontamento)
    {
        ArgumentNullException.ThrowIfNull(apontamento);
        var dia = apontamento.Dia == 0 ? UltimoUtil : Util(apontamento.Dia);
        return apontamento.Limite switch
        {
            ApontamentoFicticio.Mes => dia < InicioDoMes(0) ? UltimoUtil : dia,
            ApontamentoFicticio.Semana => dia < UltimoUtil && dia >= Segunda(UltimoUtil) ? dia : (DateOnly?)null,
            _ => dia,
        };
    }

    private static DateOnly Segunda(DateOnly dia) => dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7));

    private static bool FimDeSemana(DateOnly dia) => dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}
