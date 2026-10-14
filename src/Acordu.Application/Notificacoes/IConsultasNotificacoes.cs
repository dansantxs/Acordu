using Acordu.Application.Common;

namespace Acordu.Application.Notificacoes;

/// <summary>Notificações do usuário no tenant do contexto, mais recentes primeiro; marcar como lida só as dele.</summary>
public interface IConsultasNotificacoes
{
    Task<NotificacoesDto> ListarAsync(Guid usuarioId, bool apenasNaoLidas, Paginacao paginacao, CancellationToken cancellationToken = default);

    /// <summary>Falso se a notificação não existe ou não é do usuário.</summary>
    Task<bool> MarcarLidaAsync(Guid usuarioId, Guid notificacaoId, DateTimeOffset agora, CancellationToken cancellationToken = default);

    Task<int> MarcarTodasAsync(Guid usuarioId, DateTimeOffset agora, CancellationToken cancellationToken = default);
}
