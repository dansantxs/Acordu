using Acordu.Application.Contratos;

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
public sealed record ConfiguracaoPortalDto(string NomeTenant, string? NomeExibido, int DiasFechamentoAutomatico, Uri? LogoUrl);

public sealed record ConfiguracaoPortalRequest(string? NomeExibido, int DiasFechamentoAutomatico);

/// <summary>
/// Empresa (contrato §6, F5a): nome, endereço que recebe as respostas dos e-mails ao contato e SLA sugerido nos
/// contratos novos (o da empresa ou, sem ele, o do sistema; <see cref="SlaPersonalizado"/> diz qual). F15b: o endereço de
/// entrada por e-mail (nulo na demonstração e sem o domínio de entrada configurado) e se o recebimento está ligado
/// (F15-95, F15-96).
/// </summary>
public sealed record ConfiguracaoEmpresaDto(
    string Nome,
    string? ResponderPara,
    IReadOnlyList<PrazoSlaDto> SlaPadrao,
    bool SlaPersonalizado,
    string? EnderecoEntrada,
    bool RecebimentoEmail);

public sealed record ConfiguracaoEmpresaRequest(string? Nome, string? ResponderPara, IReadOnlyList<PrazoSlaDto>? SlaPadrao);

/// <summary>Corpo do <c>PUT /configuracoes/recebimento-email</c> (F15-03, F15-04): <c>ligado</c> obrigatório.</summary>
public sealed record RecebimentoEmailRequest(bool? Ligado);

/// <summary>Resposta do <c>PUT /configuracoes/recebimento-email</c>: o endereço de entrada e se o recebimento ficou ligado.</summary>
public sealed record RecebimentoEmailDto(string? EnderecoEntrada, bool RecebimentoEmail);
