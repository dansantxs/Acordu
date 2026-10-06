using Acordu.Domain.Entities;

namespace Acordu.Application.Abstractions;

/// <summary>Calendário de horas úteis do tenant do contexto (um por tenant, semeado no registro).</summary>
public interface ICalendarios
{
    Task<CalendarioUtil> DoTenantAsync(CancellationToken cancellationToken = default);
}
