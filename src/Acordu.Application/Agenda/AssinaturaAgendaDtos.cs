namespace Acordu.Application.Agenda;

/// <summary>Situação do link da agenda assinável da própria pessoa (contrato §11d); o link em si só aparece ao emitir.</summary>
public sealed record AssinaturaAgendaDto(bool Ativa, bool Discreta, DateTimeOffset? CriadaEm);

/// <summary>Link recém-emitido: a única vez em que a URL com o token aparece.</summary>
public sealed record LinkAgendaDto(Uri Url, bool Discreta);

/// <summary>
/// Corpo do <c>POST</c> e do <c>PATCH /agenda/assinatura</c>. No modo discreto os eventos saem com títulos genéricos
/// ("Tarefa", "Prazo de solicitação"), sem cliente nem descrição.
/// </summary>
public sealed record AssinaturaAgendaRequest(bool Discreta);
