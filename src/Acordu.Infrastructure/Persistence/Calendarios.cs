using Acordu.Application.Abstractions;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence;

/// <summary>Calendário do tenant do contexto; se ainda não existir (dados anteriores ao registro), o padrão.</summary>
public sealed class Calendarios : ICalendarios
{
    private readonly AcorduDbContext _db;
    private readonly ContextoTenant _contexto;
    private CalendarioUtil? _calendario;

    public Calendarios(AcorduDbContext db, ContextoTenant contexto)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(contexto);
        _db = db;
        _contexto = contexto;
    }

    public async Task<CalendarioUtil> DoTenantAsync(CancellationToken cancellationToken = default)
    {
        var tenant = _contexto.TenantId ?? throw new IsolamentoVioladoException("Calendário sem tenant no contexto.");
        return _calendario ??= await _db.Calendarios.AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? CalendarioUtil.Padrao(tenant);
    }

    /// <summary>Rastreado; passa a ser também o calendário que o resto da requisição enxerga.</summary>
    public async Task<CalendarioUtil> ParaEdicaoAsync(CancellationToken cancellationToken = default)
    {
        var tenant = _contexto.TenantId ?? throw new IsolamentoVioladoException("Calendário sem tenant no contexto.");
        var calendario = await _db.Calendarios.SingleOrDefaultAsync(cancellationToken);
        if (calendario is null)
        {
            calendario = CalendarioUtil.Padrao(tenant);
            _db.Calendarios.Add(calendario);
        }

        return _calendario = calendario;
    }
}
