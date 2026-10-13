using Acordu.Domain.Common;
using Acordu.Domain.Enums;

namespace Acordu.Domain.Events;

/// <summary>
/// Solicitação registrada (equipe ou portal). Reação: e-mail "recebida #N" ao contato e aviso interno, sem avisar quem
/// abriu (<see cref="AbertaPor"/>: pessoa da equipe; nulo pelo portal).
/// </summary>
public sealed record SolicitacaoAberta(
    Guid SolicitacaoId,
    Guid TenantId,
    Guid ClienteId,
    int Numero,
    OrigemSolicitacao Origem,
    DateTimeOffset Em,
    Guid? AbertaPor = null) : IEventoDominio;

/// <summary>
/// Emitido em toda mudança de status interno, inclusive resolver, reabrir, fechar e cancelar.
/// Notificar o contato só quando <see cref="ExternoMudou"/>.
/// </summary>
public sealed record SolicitacaoStatusAlterado(
    Guid SolicitacaoId,
    Guid TenantId,
    Guid ClienteId,
    StatusSolicitacao De,
    StatusSolicitacao Para,
    StatusExterno ExternoDe,
    StatusExterno ExternoPara,
    DateTimeOffset Em) : IEventoDominio
{
    public bool ExternoMudou => ExternoDe != ExternoPara;
}

/// <summary>Resolvida: e-mail com link de aprovação e agendamento do fechamento automático (RF-SOL-09).</summary>
public sealed record SolicitacaoResolvida(Guid SolicitacaoId, Guid TenantId, Guid ClienteId, DateTimeOffset Em) : IEventoDominio;

public sealed record SolicitacaoReaberta(
    Guid SolicitacaoId, Guid TenantId, Guid ClienteId, string Motivo, DateTimeOffset Em) : IEventoDominio;

/// <summary>
/// Responsável alterado (RF-NOT-02: e-mail interno de atribuição). <see cref="ResponsavelId"/> nulo = sem responsável;
/// <see cref="AtribuidoPor"/>: quem atribuiu (ninguém é avisado de atribuir a si mesmo); <see cref="Anterior"/>: o
/// responsável de antes (auditoria).
/// </summary>
public sealed record SolicitacaoAtribuida(
    Guid SolicitacaoId, Guid TenantId, Guid ClienteId, Guid? ResponsavelId, Guid? AtribuidoPor = null, Guid? Anterior = null)
    : IEventoDominio;

public sealed record PrimeiraRespostaRegistrada(
    Guid SolicitacaoId, Guid TenantId, Guid ClienteId, DateTimeOffset Em) : IEventoDominio;
