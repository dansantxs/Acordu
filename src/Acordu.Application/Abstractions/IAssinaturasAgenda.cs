namespace Acordu.Application.Abstractions;

/// <summary>
/// Link secreto da agenda assinável (RF-COL-04, segurança §5.5): um por pessoa e tenant, com só o hash do token no banco.
/// Emitir de novo invalida o anterior. Emitir, trocar o modo e revogar só preparam a mudança: grava o próximo
/// <c>IUnitOfWork.SalvarAsync</c>, junto com a auditoria.
/// </summary>
public interface IAssinaturasAgenda
{
    /// <summary>Gera o token (devolvido uma única vez) e substitui o anterior da pessoa no tenant do contexto.</summary>
    Task<string> EmitirAsync(Guid usuarioId, bool discreta, CancellationToken cancellationToken = default);

    Task<AssinaturaAgenda?> DaPessoaAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Troca o modo sem trocar o link; falso se a pessoa não tem link.</summary>
    Task<bool> AlterarModoAsync(Guid usuarioId, bool discreta, CancellationToken cancellationToken = default);

    /// <summary>Falso se a pessoa não tinha link.</summary>
    Task<bool> RevogarAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>De quem é o token (em qualquer tenant), para servir o feed sem sessão; nulo se não existe.</summary>
    Task<AssinaturaResolvida?> ResolverAsync(string token, CancellationToken cancellationToken = default);
}

public sealed record AssinaturaAgenda(bool Discreta, DateTimeOffset CriadaEm);

public sealed record AssinaturaResolvida(Guid TenantId, Guid UsuarioId, bool Discreta);
