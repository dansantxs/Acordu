using Acordu.Domain.Entities;

namespace Acordu.Application.EntradaEmail;

/// <summary>
/// Leitura da <see cref="ConversaPorEmail"/> de uma solicitação (design do F15, D3 e D9), no tenant do contexto e sob os
/// filtros globais. O que voltar é rastreado; a criação é pelo repositório e pela unidade de trabalho.
/// </summary>
public interface IConversasPorEmail
{
    /// <summary>A conversa da solicitação, ou nula se ela ainda não tem uma.</summary>
    Task<ConversaPorEmail?> DaSolicitacaoAsync(Guid solicitacaoId, CancellationToken cancellationToken = default);
}
