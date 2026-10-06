using Acordu.Domain.Entities;

namespace Acordu.Application.AcessosPortal;

/// <summary>
/// Leituras e efeitos do acesso de contatos ao portal que não cabem no repositório genérico (segurança §3.3). Tudo no
/// tenant do contexto; o que voltar é rastreado e gravado pela unidade de trabalho do caso de uso.
/// </summary>
public interface IAcessosDoPortal
{
    /// <summary>Convites do contato ainda pendentes (nem aceitos nem revogados).</summary>
    Task<IReadOnlyList<ConvitePortal>> ConvitesPendentesAsync(Guid contatoId, CancellationToken cancellationToken = default);

    /// <summary>Links de acesso por solicitação do contato ainda não revogados.</summary>
    Task<IReadOnlyList<AcessoPortal>> LinksNaoRevogadosAsync(Guid contatoId, CancellationToken cancellationToken = default);

    /// <summary>Vínculo do usuário no tenant do contexto, se houver.</summary>
    Task<Vinculo?> VinculoAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Revoga os refresh tokens ativos do usuário no tenant do contexto.</summary>
    Task RevogarSessoesAsync(Guid usuarioId, DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>Existe usuário com este e-mail que já tem vínculo no tenant (equipe ou contato de outro cliente)?</summary>
    Task<bool> EmailJaVinculadoAsync(string email, CancellationToken cancellationToken = default);
}
