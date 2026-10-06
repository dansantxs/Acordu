namespace Acordu.Application.Configuracoes;

/// <summary>Dia da semana em português no contrato (<c>SEGUNDA</c>); o domínio usa <see cref="DayOfWeek"/>.</summary>
public enum DiaDaSemana
{
    Domingo = 0,
    Segunda = 1,
    Terca = 2,
    Quarta = 3,
    Quinta = 4,
    Sexta = 5,
    Sabado = 6,
}

/// <summary>Calendário de horas úteis (contrato §6): horários em <c>HH:mm</c>, hora local do tenant.</summary>
public sealed record CalendarioDto(string FusoHorario, IReadOnlyList<JanelaDto> Expediente, IReadOnlyList<DateOnly> Feriados);

public sealed record JanelaDto(DiaDaSemana DiaSemana, string Inicio, string Fim);

/// <summary>Corpo do <c>PUT /configuracoes/calendario</c>: dias ausentes do expediente são não úteis.</summary>
public sealed record CalendarioRequest(string FusoHorario, IReadOnlyList<JanelaDto>? Expediente, IReadOnlyList<DateOnly>? Feriados);

/// <summary>Configurações do portal (contrato §6). O logotipo entra no marco de anexos.</summary>
public sealed record ConfiguracaoPortalDto(string NomeTenant, string? NomeExibido, int DiasFechamentoAutomatico);

public sealed record ConfiguracaoPortalRequest(string? NomeExibido, int DiasFechamentoAutomatico);
