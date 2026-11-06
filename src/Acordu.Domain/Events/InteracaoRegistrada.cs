using Acordu.Domain.Common;
using Acordu.Domain.Enums;

namespace Acordu.Domain.Events;

/// <summary>
/// Gatilho das regras entre agregados (modelo §3.1): interação EXTERNA da equipe registra a primeira resposta;
/// resposta do cliente em AGUARDANDO_CLIENTE retoma a solicitação; notificar a outra parte. <see cref="PedeRetorno"/>
/// (F11-04): resposta enviada com "Enviar e aguardar", cujo e-mail ao contato pede o retorno; vem da ação, e não do
/// status lido depois, para uma nova tentativa da outbox não mudar o e-mail.
/// </summary>
public sealed record InteracaoRegistrada(
    Guid InteracaoId,
    Guid TenantId,
    Guid ClienteId,
    Guid SolicitacaoId,
    Guid AutorId,
    bool AutorEhCliente,
    Visibilidade Visibilidade,
    DateTimeOffset Em,
    bool PedeRetorno = false) : IEventoDominio;
