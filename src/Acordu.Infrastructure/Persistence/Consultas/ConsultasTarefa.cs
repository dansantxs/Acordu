using System.Globalization;
using Acordu.Application.Common;
using Acordu.Application.Tarefas;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>Lista (quadro) e detalhe de tarefas, pelos filtros globais.</summary>
public sealed class ConsultasTarefa : IConsultasTarefa
{
    private readonly AcorduDbContext _db;
    private readonly ConsultaSql _sql;

    public ConsultasTarefa(AcorduDbContext db, ConsultaSql sql)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(sql);
        _db = db;
        _sql = sql;
    }

    public async Task<ResultadoPaginado<TarefaResumoDto>> ListarAsync(
        FiltroTarefas filtro, Paginacao paginacao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        IQueryable<Tarefa> consulta = _db.Tarefas.AsNoTracking();
        if (filtro.Status is { Count: > 0 } status)
            consulta = consulta.Where(t => status.Contains(t.Status));
        if (filtro.ResponsavelId is { } responsavelId)
            consulta = consulta.Where(t => t.ResponsavelId == responsavelId);
        if (filtro.SolicitacaoId is { } solicitacaoId)
            consulta = consulta.Where(t => t.SolicitacaoId == solicitacaoId);
        if (filtro.ProjetoId is { } projetoId)
            consulta = consulta.Where(t => t.ProjetoId == projetoId);
        if (filtro.ClienteId is { } clienteId)
            consulta = consulta.Where(t => t.ClienteId == clienteId);
        if (filtro.PrazoAte is { } prazoAte)
            consulta = consulta.Where(t => t.Prazo != null && t.Prazo <= prazoAte);

        var total = await consulta.CountAsync(cancellationToken);
        var pagina = await consulta
            .OrderBy(t => t.Prazo == null).ThenBy(t => t.Prazo).ThenBy(t => t.Id)
            .Skip(paginacao.Pular).Take(paginacao.TamanhoPagina)
            .ToListAsync(cancellationToken);
        var pessoas = await Nomes.DeUsuariosAsync(_db, pagina.Where(t => t.ResponsavelId is not null).Select(t => t.ResponsavelId!.Value), cancellationToken);
        return new ResultadoPaginado<TarefaResumoDto>(
            [.. pagina.Select(t => new TarefaResumoDto(
                t.Id, t.Titulo, t.Status, t.ClienteId, t.SolicitacaoId, t.ProjetoId, Nomes.Pessoa(t.ResponsavelId, pessoas), t.Prazo,
                t.Estimativa?.Minutos, t.VisivelCliente))],
            paginacao.Pagina,
            paginacao.TamanhoPagina,
            total);
    }

    public async Task<TarefaDto?> DetalharAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var linha = await _db.Tarefas.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new { Tarefa = t, Versao = EF.Property<uint>(t, ConfiguracaoRaiz.Versao) })
            .SingleOrDefaultAsync(cancellationToken);
        if (linha is null)
            return null;

        var t = linha.Tarefa;
        var pessoas = await Nomes.DeUsuariosAsync(_db, t.ResponsavelId is { } r ? [r] : [], cancellationToken);
        var apontado = (await _sql.ListarAsync<Soma>(e => $"""
            SELECT coalesce(sum(duracao_minutos), 0)::int AS total
            FROM apontamentos
            WHERE tenant_id = {e.TenantId} AND tarefa_id = {id}
              AND (CAST({e.ClienteId} AS uuid) IS NULL OR cliente_id = {e.ClienteId})
            """, cancellationToken)).Single().Total;

        return new TarefaDto(
            t.Id,
            linha.Versao.ToString(CultureInfo.InvariantCulture),
            t.Titulo,
            t.Descricao,
            t.Status,
            t.ClienteId,
            t.SolicitacaoId,
            t.ProjetoId,
            t.RecorrenciaId,
            Nomes.Pessoa(t.ResponsavelId, pessoas),
            t.Prazo,
            t.Estimativa?.Minutos,
            t.VisivelCliente,
            apontado,
            [.. t.Checklist.Select(i => new ItemChecklistDto(i.Id, i.Ordem, i.Texto, i.Feito))]);
    }

    private sealed class Soma
    {
        public int Total { get; set; }
    }
}
