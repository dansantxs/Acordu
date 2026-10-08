using Acordu.Domain.Common;

namespace Acordu.Domain.Events;

/// <summary>Convite do portal criado. Reação (pós-commit): gera o token e envia o e-mail de aceite ao contato.</summary>
public sealed record ConvitePortalCriado(
    Guid ConviteId, Guid TenantId, Guid ClienteId, Guid ContatoId, DateTimeOffset Em) : IEventoDominio;

/// <summary>Convite para a equipe criado. Reação (pós-commit): gera o token e envia o e-mail de aceite.</summary>
public sealed record ConviteEquipeCriado(Guid ConviteId, Guid TenantId, DateTimeOffset Em) : IEventoDominio;
