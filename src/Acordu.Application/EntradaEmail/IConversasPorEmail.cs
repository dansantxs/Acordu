using Acordu.Domain.Entities;

namespace Acordu.Application.EntradaEmail;

/// <summary>
/// Leitura da <see cref="ConversaPorEmail"/> de uma solicitação ou do token do endereço (design do F15, D3 e D9), no
/// tenant do contexto e sob os filtros globais. A da solicitação volta rastreada; a do token, não (só aponta a
/// solicitação). A criação é pelo repositório e pela unidade de trabalho.
/// </summary>
public interface IConversasPorEmail
{
    /// <summary>A conversa da solicitação, ou nula se ela ainda não tem uma.</summary>
    Task<ConversaPorEmail?> DaSolicitacaoAsync(Guid solicitacaoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A conversa do token do endereço, procurado só dentro da empresa do código (D3, F15-40): o token de outra empresa,
    /// adulterado ou de uma conversa que não existe mais devolve nulo.
    /// </summary>
    Task<ConversaPorEmail?> PorTokenAsync(string token, CancellationToken cancellationToken = default);
}
