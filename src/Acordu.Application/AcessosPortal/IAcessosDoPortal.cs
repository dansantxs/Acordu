using Acordu.Domain.Entities;

namespace Acordu.Application.AcessosPortal;

/// <summary>
/// Convites e links do portal que não cabem no repositório genérico (segurança §3.3). Tudo no tenant do contexto; o
/// que voltar é rastreado e gravado pela unidade de trabalho do caso de uso. Vínculos e sessões: <c>IVinculos</c>.
/// </summary>
public interface IAcessosDoPortal
{
    /// <summary>Convites do contato ainda pendentes (nem aceitos nem revogados).</summary>
    Task<IReadOnlyList<ConvitePortal>> ConvitesPendentesAsync(Guid contatoId, CancellationToken cancellationToken = default);

    /// <summary>Links de acesso por solicitação do contato ainda não revogados.</summary>
    Task<IReadOnlyList<AcessoPortal>> LinksNaoRevogadosAsync(Guid contatoId, CancellationToken cancellationToken = default);
}
