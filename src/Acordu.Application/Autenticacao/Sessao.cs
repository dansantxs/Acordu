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
