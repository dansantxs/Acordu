namespace Acordu.Domain.Common;

/// <summary>
/// Fato relevante que aconteceu no domínio. Os agregados só registram eventos;
/// quem reage (notificações, regras entre agregados) é a Application.
/// </summary>
public interface IEventoDominio;
