using Acordu.Domain.Enums;

namespace Acordu.Application.Abstractions;

/// <summary>
/// Quem está agindo, resolvido do token pela Api (ADR 0002). É a fonte dos dois isolamentos: por tenant, sempre, e por
/// cliente, quando o papel é Cliente. Sem tenant (nulo), as consultas não devolvem nada e nada pode ser gravado.
/// Jobs agem como o sistema dentro de um tenant: sem usuário e sem permissões. A sessão de link por solicitação também
/// não tem usuário: tem cliente, o acesso que a abriu e a única solicitação permitida.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }

    /// <summary>Papel Cliente (contato do portal): ativa o filtro por <see cref="ClienteId"/>.</summary>
    bool EhCliente { get; }

    /// <summary>Cliente do contato; obrigatório quando <see cref="EhCliente"/>.</summary>
    Guid? ClienteId { get; }

    /// <summary>Usuário autenticado; nulo para jobs.</summary>
    Guid? UsuarioId { get; }

    /// <summary>Nome do papel no tenant ativo; nulo para jobs.</summary>
    string? Papel { get; }

    /// <summary>Permissões do papel no tenant ativo (catálogo em <c>Domain.Acesso.Permissoes</c>).</summary>
    IReadOnlySet<string> Permissoes { get; }

    /// <summary>Escopo do portal; só com papel Cliente.</summary>
    EscopoPortal? EscopoPortal { get; }

    /// <summary>Sessão de link (RF-POR-08): o <c>AcessoPortal</c> que a abriu; nulo nas demais sessões.</summary>
    Guid? AcessoPortalId { get; }

    /// <summary>Sessão de link: a única solicitação visível; nulo nas demais sessões.</summary>
    Guid? SolicitacaoPermitida { get; }
}
