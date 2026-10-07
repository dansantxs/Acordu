using Acordu.Domain.Enums;

namespace Acordu.Application.Autenticacao;

/// <summary>Sessão aberta no tenant ativo: o que vai no access token e o refresh token (só aqui em claro).</summary>
public sealed record Sessao(
    Guid UsuarioId,
    Guid TenantId,
    string Papel,
    IReadOnlyList<string> Permissoes,
    Guid? ClienteId,
    EscopoPortal? EscopoPortal,
    string RefreshToken,
    DateTimeOffset RefreshExpiraEm);

/// <summary>Sessão de link por solicitação: o que vai no access token (segurança §5.3). Não há usuário nem refresh.</summary>
public sealed record SessaoLink(Guid TenantId, Guid ClienteId, Guid AcessoPortalId, Guid SolicitacaoId);
