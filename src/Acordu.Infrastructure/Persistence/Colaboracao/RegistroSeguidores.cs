using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Colaboracao;

/// <summary>Seguidores no tenant do contexto: seguir grava na hora e é idempotente por (tenant, alvo, id, pessoa).</summary>
public sealed class RegistroSeguidores : ISeguidores
{
    private readonly AcorduDbContext _db;
    private readonly ITenantContext _contexto;
    private readonly TimeProvider _relogio;

    public RegistroSeguidores(AcorduDbContext db, ITenantContext contexto, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(relogio);
        _db = db;
        _contexto = contexto;
        _relogio = relogio;
    }

    public async Task SeguirAsync(AlvoColaboracao alvo, Guid alvoId, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var tenant = _contexto.TenantId ?? throw new InvalidOperationException("Seguir exige tenant no contexto.");
        var agora = _relogio.GetUtcNow();
        var tipo = (int)alvo;
        await _db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO seguidores (tenant_id, alvo, alvo_id, usuario_id, desde)
            VALUES ({tenant}, {tipo}, {alvoId}, {usuarioId}, {agora})
            ON CONFLICT (tenant_id, alvo, alvo_id, usuario_id) DO NOTHING
            """,
            cancellationToken);
    }

    public Task DeixarAsync(AlvoColaboracao alvo, Guid alvoId, Guid usuarioId, CancellationToken cancellationToken = default) =>
        _db.Set<Seguidor>()
            .Where(s => s.Alvo == alvo && s.AlvoId == alvoId && s.UsuarioId == usuarioId)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> DoAlvoAsync(AlvoColaboracao alvo, Guid alvoId, CancellationToken cancellationToken = default) =>
        await _db.Set<Seguidor>().AsNoTracking()
            .Where(s => s.Alvo == alvo && s.AlvoId == alvoId)
            .OrderBy(s => s.Desde).ThenBy(s => s.UsuarioId)
            .Select(s => s.UsuarioId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Referencia>> ListarAsync(AlvoColaboracao alvo, Guid alvoId, CancellationToken cancellationToken = default) =>
        await (from s in _db.Set<Seguidor>().AsNoTracking()
               join u in _db.Users on s.UsuarioId equals u.Id
               where s.Alvo == alvo && s.AlvoId == alvoId
               orderby u.Nome, u.Id
               select new Referencia(u.Id, u.Nome))
            .ToListAsync(cancellationToken);
}
