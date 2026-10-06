using Acordu.Domain.Enums;

namespace Acordu.Application.Autenticacao;

/// <summary>Resposta do <c>GET /auth/eu</c> (contrato §2).</summary>
public sealed record PerfilUsuario(
    Guid UsuarioId,
    string Nome,
    string Email,
    TenantAtivo Tenant,
    string Papel,
    IReadOnlyList<string> Permissoes,
    Guid? ClienteId,
    EscopoPortal? EscopoPortal,
    IReadOnlyList<TenantDoUsuario> Tenants);

/// <summary>Tenant ativo, com o fuso em que a UI exibe instantes (convenções §4).</summary>
public sealed record TenantAtivo(Guid Id, string Nome, string FusoHorario);

/// <summary>Um tenant em que o usuário tem vínculo, para a troca de tenant.</summary>
public sealed record TenantDoUsuario(Guid Id, string Nome, string Papel);
