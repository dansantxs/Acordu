using System.Globalization;
using Acordu.Application.Common;
using Acordu.Application.Recorrencias;
using Acordu.Domain.Enums;
using Acordu.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>Recorrências pelos filtros globais. O modelo é jsonb com conversor: projetado em memória, depois da página.</summary>
public sealed class ConsultasRecorrencia : IConsultasRecorrencia
{
    private readonly AcorduDbContext _db;
    private readonly ConsultaSql _sql;

    public ConsultasRecorrencia(AcorduDbContext db, ConsultaSql sql)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(sql);
        _db = db;
        _sql = sql;
    }

    /// <summary>SQL cru pela aritmética de datas (<c>date - integer</c>); o índice (status, proxima_data) cobre a varredura.</summary>
    public async Task<IReadOnlyList<Guid>> DevidasAsync(DateOnly hoje, CancellationToken cancellationToken = default) =>
        [.. (await _sql.ListarAsync<RecorrenciaDevida>(e => $"""
            SELECT id
            FROM recorrencias
            WHERE tenant_id = {e.TenantId} AND status = 'ATIVA' AND proxima_data IS NOT NULL
              AND proxima_data - regra_antecedencia_dias <= {hoje}
              AND (CAST({e.ClienteId} AS uuid) IS NULL OR cliente_id = {e.ClienteId})
            ORDER BY proxima_data, id
            """, cancellationToken)).Select(r => r.Id)];

    public async Task<ResultadoPaginado<RecorrenciaResumoDto>> ListarAsync(
        Guid? clienteId, StatusRecorrencia? status, Paginacao paginacao, CancellationToken cancellationToken = default)
    {
        var consulta = from r in _db.Recorrencias.AsNoTracking()
                       join cl in _db.Clientes on r.ClienteId equals cl.Id
                       where (clienteId == null || r.ClienteId == clienteId) && (status == null || r.Status == status)
                       select new { r, ClienteNome = cl.Nome };
        var total = await consulta.CountAsync(cancellationToken);
        // Ativas primeiro, pela próxima data (sem próxima por último).
        var linhas = await consulta
            .OrderBy(x => x.r.Status).ThenBy(x => x.r.ProximaData == null).ThenBy(x => x.r.ProximaData).ThenBy(x => x.r.Id)
            .Skip(paginacao.Pular).Take(paginacao.TamanhoPagina)
            .ToListAsync(cancellationToken);
        return new ResultadoPaginado<RecorrenciaResumoDto>(
            [.. linhas.Select(x => new RecorrenciaResumoDto(
                x.r.Id, x.r.ClienteId, x.ClienteNome, x.r.ContratoId, x.r.Modelo.Titulo, x.r.Modelo.ResponsavelId,
                x.r.Regra.Periodicidade, x.r.ProximaData, x.r.Status))],
            paginacao.Pagina,
            paginacao.TamanhoPagina,
            total);
    }

    public async Task<RecorrenciaDto?> DetalharAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var linha = await (from r in _db.Recorrencias.AsNoTracking()
                           join cl in _db.Clientes on r.ClienteId equals cl.Id
                           where r.Id == id
                           select new { r, ClienteNome = cl.Nome, Versao = EF.Property<uint>(r, ConfiguracaoRaiz.Versao) })
            .SingleOrDefaultAsync(cancellationToken);
        if (linha is null)
            return null;

        var recorrencia = linha.r;
        var modelo = recorrencia.Modelo;
        var regra = recorrencia.Regra;
        return new RecorrenciaDto(
            recorrencia.Id,
            recorrencia.ClienteId,
            linha.ClienteNome,
            recorrencia.ContratoId,
            new ModeloTarefaDto(
                modelo.Titulo, modelo.Descricao, modelo.ResponsavelId, modelo.Estimativa?.Minutos, modelo.VisivelCliente,
                [.. modelo.Checklist]),
            new RegraRecorrenciaDto(regra.Periodicidade, regra.DiaReferencia, regra.AntecedenciaDias, regra.Inicio, regra.Fim),
            recorrencia.ProximaData,
            recorrencia.Status,
            linha.Versao.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Linha da varredura do agendador.</summary>
    private sealed class RecorrenciaDevida
    {
        public Guid Id { get; set; }
    }
}
