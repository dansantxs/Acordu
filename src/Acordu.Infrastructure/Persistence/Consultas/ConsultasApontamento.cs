using System.Globalization;
using Acordu.Application.Apontamentos;
using Acordu.Application.Common;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>Apontamentos e cronômetro, pelos filtros globais.</summary>
public sealed class ConsultasApontamento : IConsultasApontamento
{
    private readonly AcorduDbContext _db;
    private readonly TimeProvider _relogio;

    public ConsultasApontamento(AcorduDbContext db, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(relogio);
        _db = db;
        _relogio = relogio;
    }

    public async Task<ResultadoPaginado<ApontamentoDto>> ListarAsync(
        FiltroApontamentos filtro, Paginacao paginacao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        IQueryable<Apontamento> consulta = _db.Apontamentos.AsNoTracking();
        if (filtro.UsuarioId is { } usuarioId)
            consulta = consulta.Where(a => a.UsuarioId == usuarioId);
        if (filtro.ClienteId is { } clienteId)
            consulta = consulta.Where(a => a.ClienteId == clienteId);
        if (filtro.ContratoId is { } contratoId)
            consulta = consulta.Where(a => a.ContratoId == contratoId);
        if (filtro.TarefaId is { } tarefaId)
            consulta = consulta.Where(a => a.TarefaId == tarefaId);
        if (filtro.De is { } de)
            consulta = consulta.Where(a => a.Data >= de);
        if (filtro.Ate is { } ate)
            consulta = consulta.Where(a => a.Data <= ate);
        if (filtro.Faturavel is { } faturavel)
            consulta = consulta.Where(a => a.Faturavel == faturavel);

        var total = await consulta.CountAsync(cancellationToken);
        var linhas = await consulta
            .OrderByDescending(a => a.Data).ThenByDescending(a => a.Id)
            .Skip(paginacao.Pular).Take(paginacao.TamanhoPagina)
            .Select(a => new { Apontamento = a, Versao = EF.Property<uint>(a, ConfiguracaoRaiz.Versao) })
            .ToListAsync(cancellationToken);
        var itens = await MontarAsync([.. linhas.Select(l => (l.Apontamento, l.Versao))], cancellationToken);
        return new ResultadoPaginado<ApontamentoDto>(itens, paginacao.Pagina, paginacao.TamanhoPagina, total);
    }

    public async Task<ApontamentoDto?> DetalharAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var linha = await _db.Apontamentos.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new { Apontamento = a, Versao = EF.Property<uint>(a, ConfiguracaoRaiz.Versao) })
            .SingleOrDefaultAsync(cancellationToken);
        return linha is null ? null : (await MontarAsync([(linha.Apontamento, linha.Versao)], cancellationToken))[0];
    }

    public async Task<Guid?> CronometroDoUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        await _db.Cronometros.AsNoTracking()
            .Where(c => c.UsuarioId == usuarioId)
            .Select(c => (Guid?)c.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<CronometroDto?> CronometroAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var linha = await (from c in _db.Cronometros.AsNoTracking()
                           join t in _db.Tarefas on c.TarefaId equals t.Id into tarefas
                           from t in tarefas.DefaultIfEmpty()
                           where c.UsuarioId == usuarioId
                           select new { c.Id, c.TarefaId, c.IniciadoEm, Titulo = t == null ? null : t.Titulo })
            .SingleOrDefaultAsync(cancellationToken);
        if (linha is null)
            return null;

        var decorrido = (int)Math.Floor((_relogio.GetUtcNow() - linha.IniciadoEm).TotalMinutes);
        return new CronometroDto(linha.Id, new Referencia(linha.TarefaId, linha.Titulo ?? string.Empty), linha.IniciadoEm, Math.Max(decorrido, 0));
    }

    private async Task<List<ApontamentoDto>> MontarAsync(
        IReadOnlyList<(Apontamento Apontamento, uint Versao)> linhas, CancellationToken cancellationToken)
    {
        var tarefaIds = linhas.Select(l => l.Apontamento.TarefaId).Distinct().ToList();
        var titulos = await _db.Tarefas.AsNoTracking()
            .Where(t => tarefaIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Titulo, cancellationToken);
        var pessoas = await Nomes.DeUsuariosAsync(_db, linhas.Select(l => l.Apontamento.UsuarioId), cancellationToken);
        return [.. linhas.Select(l =>
        {
            var a = l.Apontamento;
            return new ApontamentoDto(
                a.Id,
                l.Versao.ToString(CultureInfo.InvariantCulture),
                new Referencia(a.TarefaId, titulos.GetValueOrDefault(a.TarefaId, string.Empty)),
                a.ClienteId,
                new Referencia(a.UsuarioId, pessoas.GetValueOrDefault(a.UsuarioId, string.Empty)),
                a.ContratoId,
                a.Data,
                a.Duracao.Minutos,
                a.Descricao,
                a.DescricaoExterna,
                a.Faturavel,
                a.VisivelCliente);
        })];
    }
}
