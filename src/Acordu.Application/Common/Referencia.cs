namespace Acordu.Application.Common;

/// <summary>Id e nome de quem é referenciado (cliente, contato, responsável, autor).</summary>
public sealed record Referencia(Guid Id, string Nome);
