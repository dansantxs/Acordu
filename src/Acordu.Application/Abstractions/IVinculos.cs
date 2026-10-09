using Acordu.Domain.Entities;

namespace Acordu.Application.Abstractions;

/// <summary>
/// Vínculos e sessões no tenant do contexto (equipe e contatos do portal). O que voltar é rastreado e gravado pela
/// unidade de trabalho do caso de uso.
/// </summary>
public interface IVinculos
{
    /// <summary>Vínculo do usuário no tenant do contexto, se houver.</summary>
    Task<Vinculo?> DoUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Quantos vínculos têm o papel padrão Dono (guarda "≥ 1 Dono por tenant").</summary>
    Task<int> ContarDonosAsync(CancellationToken cancellationToken = default);

    /// <summary>Quantos vínculos do tenant usam o papel (papel em uso não é excluído).</summary>
    Task<int> ContarPorPapelAsync(Guid papelId, CancellationToken cancellationToken = default);

    /// <summary>Revoga os refresh tokens ativos do usuário no tenant do contexto.</summary>
    Task RevogarSessoesAsync(Guid usuarioId, DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>Existe usuário com este e-mail que já tem vínculo no tenant (equipe ou contato de algum cliente)?</summary>
    Task<bool> EmailJaVinculadoAsync(string email, CancellationToken cancellationToken = default);
}
