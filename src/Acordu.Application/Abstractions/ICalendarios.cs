using Acordu.Domain.Entities;

namespace Acordu.Application.Abstractions;

/// <summary>Calendário de horas úteis do tenant do contexto (um por tenant, semeado no registro).</summary>
public interface ICalendarios
{
    Task<CalendarioUtil> DoTenantAsync(CancellationToken cancellationToken = default);

    /// <summary>O calendário rastreado para edição; se o tenant ainda não tiver um, cria o padrão.</summary>
    Task<CalendarioUtil> ParaEdicaoAsync(CancellationToken cancellationToken = default);
}
