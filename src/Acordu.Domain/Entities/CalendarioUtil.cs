using Acordu.Domain.Common;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Entities;

/// <summary>
/// Calendário de horas úteis do prestador: fuso, expediente por dia da semana e feriados.
/// Um por tenant. É a base do cálculo de SLA (ADR 0003).
/// </summary>
public sealed class CalendarioUtil : EntidadeBase
{
    public const string FusoPadrao = "America/Sao_Paulo";

    // Listas (e não dicionário/conjunto) para o EF mapear: expediente como coleção própria, feriados como date[].
    private readonly List<JanelaDiaria> _expediente = [];
    private List<DateOnly> _feriados = [];
    private TimeZoneInfo? _fuso;

    private CalendarioUtil(Guid id, Guid tenantId, string fusoHorario)
        : base(id, tenantId)
    {
        FusoHorario = fusoHorario;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private CalendarioUtil()
    {
        FusoHorario = FusoPadrao;
    }

    /// <summary>Identificador IANA do fuso (ex.: America/Sao_Paulo).</summary>
    public string FusoHorario { get; private set; }

    public IReadOnlyCollection<JanelaDiaria> Expediente => _expediente;

    public IReadOnlyCollection<DateOnly> Feriados => _feriados;

    /// <summary>Fuso resolvido a partir de <see cref="FusoHorario"/>.</summary>
    public TimeZoneInfo Fuso => _fuso ??= ResolverFuso(FusoHorario);

    public static CalendarioUtil Criar(
        Guid tenantId,
        string fusoHorario,
        IEnumerable<JanelaDiaria> expediente,
        IEnumerable<DateOnly>? feriados = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fusoHorario);
        ArgumentNullException.ThrowIfNull(expediente);

        var calendario = new CalendarioUtil(Guid.NewGuid(), tenantId, fusoHorario.Trim());
        _ = calendario.Fuso; // valida o fuso já na criação
        calendario.DefinirExpediente(expediente);
        calendario.DefinirFeriados(feriados ?? []);
        return calendario;
    }

    /// <summary>Calendário semeado no cadastro do tenant: seg–sex 08:00–18:00, America/Sao_Paulo, sem feriados.</summary>
    public static CalendarioUtil Padrao(Guid tenantId)
    {
        var inicio = new TimeOnly(8, 0);
        var fim = new TimeOnly(18, 0);
        DayOfWeek[] diasUteis = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

        return Criar(tenantId, FusoPadrao, diasUteis.Select(d => new JanelaDiaria(d, inicio, fim)));
    }

    public void AlterarFuso(string fusoHorario)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fusoHorario);
        var fuso = ResolverFuso(fusoHorario.Trim());
        FusoHorario = fusoHorario.Trim();
        _fuso = fuso;
    }

    /// <summary>Substitui o expediente. Dias ausentes são não úteis.</summary>
    public void DefinirExpediente(IEnumerable<JanelaDiaria> expediente)
    {
        ArgumentNullException.ThrowIfNull(expediente);
        var janelas = expediente.ToList();

        var repetido = janelas.GroupBy(j => j.Dia).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null)
            throw new CalendarioInvalidoException($"Há mais de uma janela de expediente para {repetido.Key}.");
        if (janelas.Count == 0)
            throw new CalendarioInvalidoException("O calendário precisa de ao menos um dia útil na semana.");

        _expediente.Clear();
        _expediente.AddRange(janelas.OrderBy(j => j.Dia));
    }

    /// <summary>Substitui a lista de feriados (duplicatas são ignoradas; guardada em ordem).</summary>
    public void DefinirFeriados(IEnumerable<DateOnly> feriados)
    {
        ArgumentNullException.ThrowIfNull(feriados);
        _feriados = [.. feriados.Distinct().Order()];
    }

    /// <summary>Janela de expediente da data, ou nula se a data não é dia útil (fim de semana sem expediente ou feriado).</summary>
    public JanelaDiaria? JanelaDe(DateOnly data) =>
        _feriados.BinarySearch(data) < 0 ? _expediente.Find(j => j.Dia == data.DayOfWeek) : null;

    private static TimeZoneInfo ResolverFuso(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException ex)
        {
            throw new CalendarioInvalidoException($"Fuso horário desconhecido: '{id}'.", ex);
        }
        catch (InvalidTimeZoneException ex)
        {
            throw new CalendarioInvalidoException($"Fuso horário inválido: '{id}'.", ex);
        }
    }
}
