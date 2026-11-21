namespace Acordu.Application.Nebrys;

/// <summary>
/// O operador da Nebrys da requisição (ADR 0014, item 6): a conta da sessão de operação, conferida pela barreira da área
/// a cada requisição. As ações da área o gravam em <c>usuario</c> na trilha sem tenant. O <c>ITenantContext</c> continua
/// vazio: a sessão de operação não tem tenant.
/// </summary>
public interface IOperadorAtual
{
    /// <summary>A conta do operador. Fora de uma rota da área (sem a conferência), lança.</summary>
    Guid Id { get; }
}
