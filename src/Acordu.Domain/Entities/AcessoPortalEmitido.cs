namespace Acordu.Domain.Entities;

/// <summary>Resultado da emissão: o acesso a persistir e o token em claro, que só existe aqui.</summary>
public sealed record AcessoPortalEmitido(AcessoPortal Acesso, string Token);
