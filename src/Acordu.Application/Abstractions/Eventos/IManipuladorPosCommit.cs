using Acordu.Domain.Common;

namespace Acordu.Application.Abstractions.Eventos;

/// <summary>
/// Efeito externo de um evento (e-mail, notificação), executado <b>depois</b> do commit a partir da outbox, no contexto do
/// tenant do evento. Entrega "pelo menos uma vez": a implementação precisa ser idempotente.
/// </summary>
public interface IManipuladorPosCommit<in TEvento>
    where TEvento : IEventoDominio
{
    Task TratarAsync(TEvento evento, CancellationToken cancellationToken);
}
