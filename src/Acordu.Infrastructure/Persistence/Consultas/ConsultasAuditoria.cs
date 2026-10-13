using System.Text.Json;
using Acordu.Application.Abstractions;
using Acordu.Application.Auditoria;
using Acordu.Application.Common;
using Acordu.Infrastructure.Persistence.Auditoria;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>Trilha do tenant pelos filtros globais, mais recentes primeiro. Datas do filtro no fuso do tenant.</summary>
public sealed class ConsultasAuditoria : IConsultasAuditoria
{
    private readonly AcorduDbContext _db;
    private readonly ICalendarios _calendarios;

    public ConsultasAuditoria(AcorduDbContext db, ICalendarios calendarios)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(calendarios);
        _db = db;
        _calendarios = calendarios;
    }

    public async Task<ResultadoPaginado<RegistroAuditoriaDto>> ListarAsync(
        FiltroAuditoria filtro, Paginacao paginacao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var consulta = _db.Set<RegistroAuditoria>().AsNoTracking();
        if (filtro.De is not null || filtro.Ate is not null)
        {
            var fuso = (await _calendarios.DoTenantAsync(cancellationToken)).Fuso;
            if (filtro.De is { } de)
            {
                var desde = Inicio(de, fuso);
                consulta = consulta.Where(r => r.OcorridoEm >= desde);
            }

            if (filtro.Ate is { } ate)
            {
                var antes = Inicio(ate.AddDays(1), fuso);
                consulta = consulta.Where(r => r.OcorridoEm < antes);
            }
        }

        if (!string.IsNullOrWhiteSpace(filtro.Acao))
        {
            var acao = filtro.Acao.Trim();
            consulta = consulta.Where(r => r.Acao == acao);
        }

        if (filtro.UsuarioId is { } usuarioId)
            consulta = consulta.Where(r => r.UsuarioId == usuarioId);
        if (filtro.AlvoId is { } alvoId)
            consulta = consulta.Where(r => r.AlvoId == alvoId);

        var total = await consulta.CountAsync(cancellationToken);
        var linhas = await (from r in consulta
                            join u in _db.Users on r.UsuarioId equals u.Id into usuarios
                            from u in usuarios.DefaultIfEmpty()
                            orderby r.OcorridoEm descending, r.Id descending
                            select new { r, Nome = u != null ? u.Nome : null })
            .Skip(paginacao.Pular).Take(paginacao.TamanhoPagina)
            .ToListAsync(cancellationToken);

        return new ResultadoPaginado<RegistroAuditoriaDto>(
            [.. linhas.Select(x => new RegistroAuditoriaDto(
                x.r.Id,
                x.r.OcorridoEm,
                x.r.Acao,
                x.r.UsuarioId is { } id ? new Referencia(id, x.Nome ?? "Usuário removido") : null,
                x.r.AlvoTipo,
                x.r.AlvoId,
                x.r.Dados is null ? null : JsonDocument.Parse(x.r.Dados).RootElement.Clone(),
                x.r.Ip))],
            paginacao.Pagina,
            paginacao.TamanhoPagina,
            total);
    }

    private static DateTimeOffset Inicio(DateOnly dia, TimeZoneInfo fuso)
    {
        var local = dia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, fuso.GetUtcOffset(local)).ToUniversalTime();
    }
}
