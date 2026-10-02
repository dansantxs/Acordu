using Acordu.Domain.Common;
using Acordu.Domain.Enums;

namespace Acordu.Domain.Events;

/// <summary>
/// Gatilho das regras entre agregados (modelo §3.1): interação EXTERNA da equipe registra a primeira resposta;
/// resposta do cliente em AGUARDANDO_CLIENTE retoma a solicitação; notificar a outra parte.
/// </summary>
public sealed record InteracaoRegistrada(
    Guid InteracaoId,
    Guid TenantId,
    Guid ClienteId,
    Guid SolicitacaoId,
    Guid AutorId,
    bool AutorEhCliente,
    Visibilidade Visibilidade,
    DateTimeOffset Em) : IEventoDominio;
