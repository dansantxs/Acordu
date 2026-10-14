using Acordu.Application.Notificacoes;

namespace Acordu.Application.Abstractions;

/// <summary>
/// Notificações do sino (RF-NOT-03) no tenant do contexto. Gravadas na hora, fora da unidade de trabalho (os avisos saem
/// depois do commit ou no job), e idempotentes por (usuário, chave): uma reentrega da outbox não duplica.
/// </summary>
public interface INotificacoes
{
    Task RegistrarAsync(Guid usuarioId, CategoriaAviso categoria, string chave, string titulo, Uri? link, CancellationToken cancellationToken = default);
}

/// <summary>Categorias de e-mail que cada pessoa desligou (RF-NOT-04), por tenant.</summary>
public interface IPreferenciasNotificacao
{
    Task<IReadOnlySet<CategoriaAviso>> EmailDesligadoAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task SalvarAsync(Guid usuarioId, IReadOnlySet<CategoriaAviso> emailDesligado, CancellationToken cancellationToken = default);
}
