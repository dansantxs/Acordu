using System.Globalization;
using Acordu.Application.Common;
using Acordu.Application.Projetos;
using Acordu.Domain.Enums;
using Acordu.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>Progresso de projeto (read side §7) numa consulta só, com o projeto lido pelos filtros globais.</summary>
public sealed class ConsultasProjeto : IConsultasProjeto
{
    private readonly AcorduDbContext _db;
    private readonly ConsultaSql _sql;

    public ConsultasProjeto(AcorduDbContext db, ConsultaSql sql)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(sql);
        _db = db;
        _sql = sql;
    }

    public async Task<ProgressoProjetoDto?> ProgressoAsync(Guid projetoId, CancellationToken cancellationToken = default)
    {
        var projeto = await _db.Projetos.AsNoTracking()
            .Where(p => p.Id == projetoId)
            .Select(p => new { p.Estimativa })
            .SingleOrDefaultAsync(cancellationToken);
        if (projeto is null)
            return null;

        var somas = (await _sql.ListarAsync<Somas>(e => $"""
            SELECT
                count(*) FILTER (WHERE t.status <> 'CANCELADA')::int AS total,
                count(*) FILTER (WHERE t.status = 'CONCLUIDA')::int AS concluidas,
                sum(t.estimativa_minutos) FILTER (WHERE t.status <> 'CANCELADA')::int AS estimativa_tarefas,
                coalesce((SELECT sum(a.duracao_minutos) FROM apontamentos a
                          WHERE a.tenant_id = {e.TenantId} AND a.tarefa_id IN
                                (SELECT id FROM tarefas WHERE tenant_id = {e.TenantId} AND projeto_id = {projetoId})), 0)::int AS apontado
            FROM tarefas t
            WHERE t.tenant_id = {e.TenantId} AND t.projeto_id = {projetoId}
              AND (CAST({e.ClienteId} AS uuid) IS NULL OR t.cliente_id = {e.ClienteId})
            """, cancellationToken)).Single();

        return new ProgressoProjetoDto(
            somas.Concluidas, somas.Total, somas.Apontado, projeto.Estimativa?.Minutos ?? somas.EstimativaTarefas);
    }

    public async Task<ResultadoPaginado<ProjetoResumoDto>> ListarAsync(
        Guid? clienteId, StatusProjeto? status, Paginacao paginacao, CancellationToken cancellationToken = default)
    {
        var consulta = from p in _db.Projetos.AsNoTracking()
                       join c in _db.Clientes on p.ClienteId equals c.Id
                       where (clienteId == null || p.ClienteId == clienteId) && (status == null || p.Status == status)
                       select new { p, ClienteNome = c.Nome };
        var total = await consulta.CountAsync(cancellationToken);
        var linhas = await consulta
            .OrderBy(x => x.p.Nome).ThenBy(x => x.p.Id)
            .Skip(paginacao.Pular).Take(paginacao.TamanhoPagina)
            .ToListAsync(cancellationToken);
        return new ResultadoPaginado<ProjetoResumoDto>(
            [.. linhas.Select(x => new ProjetoResumoDto(
                x.p.Id, x.p.Nome, new Referencia(x.p.ClienteId, x.ClienteNome), x.p.ContratoId, x.p.Prazo, x.p.Estimativa?.Minutos,
                x.p.Status))],
            paginacao.Pagina,
            paginacao.TamanhoPagina,
            total);
    }

    public async Task<ProjetoDto?> DetalharAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var linha = await (from p in _db.Projetos.AsNoTracking()
                           join c in _db.Clientes on p.ClienteId equals c.Id
                           where p.Id == id
                           select new { p, ClienteNome = c.Nome, Versao = EF.Property<uint>(p, ConfiguracaoRaiz.Versao) })
            .SingleOrDefaultAsync(cancellationToken);
        if (linha is null)
            return null;

        var projeto = linha.p;
        return new ProjetoDto(
            projeto.Id,
            linha.Versao.ToString(CultureInfo.InvariantCulture),
            projeto.Nome,
            new Referencia(projeto.ClienteId, linha.ClienteNome),
            projeto.ContratoId,
            projeto.Prazo,
            projeto.Estimativa?.Minutos,
            projeto.Status,
            (await ProgressoAsync(id, cancellationToken))!);
    }

    private sealed class Somas
    {
        public int Total { get; set; }

        public int Concluidas { get; set; }

        public int? EstimativaTarefas { get; set; }

        public int Apontado { get; set; }
    }
}
