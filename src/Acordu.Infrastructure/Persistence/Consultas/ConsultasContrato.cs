using Acordu.Application.Contratos;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>Consumo de contrato pelas fórmulas do read side §3, somando minutos no banco (index-only scan no índice com INCLUDE).</summary>
public sealed class ConsultasContrato : IConsultasContrato
{
    private readonly AcorduDbContext _db;
    private readonly ConsultaSql _sql;

    public ConsultasContrato(AcorduDbContext db, ConsultaSql sql)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(sql);
        _db = db;
        _sql = sql;
    }

    public async Task<ConsumoContratoDto?> ConsumoAsync(
        Guid contratoId, MesReferencia periodo, DateOnly hoje, CancellationToken cancellationToken = default)
    {
        // Leitura do contrato pelos filtros globais: outro tenant ou outro cliente → nulo.
        var contrato = await _db.Contratos.AsNoTracking()
            .Where(c => c.Id == contratoId)
            .Select(c => new { c.Tipo, c.Franquia })
            .SingleOrDefaultAsync(cancellationToken);
        if (contrato is null)
            return null;

        var inicio = periodo.PrimeiroDia;
        var fim = periodo.UltimoDia;
        var ate = hoje < fim ? hoje : fim;

        var horas = (await _sql.ListarAsync<SomasApontamentos>(e => $"""
            SELECT
                coalesce(sum(duracao_minutos) FILTER (WHERE faturavel AND data BETWEEN {inicio} AND {fim}), 0)::int AS consumido,
                coalesce(sum(duracao_minutos) FILTER (WHERE NOT faturavel AND data BETWEEN {inicio} AND {fim}), 0)::int AS nao_faturavel,
                coalesce(sum(duracao_minutos) FILTER (WHERE faturavel AND data <= {ate}), 0)::int AS consumido_acumulado
            FROM apontamentos
            WHERE tenant_id = {e.TenantId} AND contrato_id = {contratoId}
              AND (CAST({e.ClienteId} AS uuid) IS NULL OR cliente_id = {e.ClienteId})
            """, cancellationToken)).Single();

        var ajustes = (await _sql.ListarAsync<SomaAjustes>(e => $"""
            SELECT
                coalesce(sum(quantidade_minutos) FILTER (WHERE data BETWEEN {inicio} AND {fim}), 0)::int AS no_periodo,
                coalesce(sum(quantidade_minutos) FILTER (WHERE data <= {ate}), 0)::int AS acumulado
            FROM ajustes_saldo
            WHERE tenant_id = {e.TenantId} AND contrato_id = {contratoId}
              AND (CAST({e.ClienteId} AS uuid) IS NULL OR cliente_id = {e.ClienteId})
            """, cancellationToken)).Single();

        var franquia = contrato.Franquia?.Minutos;
        return contrato.Tipo switch
        {
            TipoContrato.MensalFixo => Mensal(contratoId, periodo, franquia, horas, ajustes.NoPeriodo),
            TipoContrato.BancoHoras => BancoDeHoras(contratoId, periodo, franquia ?? 0, horas, ajustes.Acumulado),
            _ => new ConsumoContratoDto(
                contratoId, contrato.Tipo, periodo, null, horas.Consumido, horas.NaoFaturavel, 0, null, null, null),
        };
    }

    private static ConsumoContratoDto Mensal(Guid id, MesReferencia periodo, int? franquia, SomasApontamentos horas, int ajustes)
    {
        int? saldo = franquia is { } f ? f + ajustes - horas.Consumido : null;
        int? percentual = franquia is { } g ? Percentual(horas.Consumido, g + ajustes) : null;
        return new ConsumoContratoDto(
            id, TipoContrato.MensalFixo, periodo, franquia, horas.Consumido, horas.NaoFaturavel, ajustes, null, saldo, percentual);
    }

    private static ConsumoContratoDto BancoDeHoras(Guid id, MesReferencia periodo, int franquia, SomasApontamentos horas, int ajustes) =>
        new(
            id,
            TipoContrato.BancoHoras,
            periodo,
            franquia,
            horas.Consumido,
            horas.NaoFaturavel,
            ajustes,
            horas.ConsumidoAcumulado,
            franquia + ajustes - horas.ConsumidoAcumulado,
            Percentual(horas.ConsumidoAcumulado, franquia + ajustes));

    /// <summary>Arredonda para baixo; base zero ou negativa (franquia estornada) não tem percentual.</summary>
    private static int? Percentual(int consumido, int base_) => base_ > 0 ? (int)(consumido * 100L / base_) : null;

    private sealed class SomasApontamentos
    {
        public int Consumido { get; set; }

        public int NaoFaturavel { get; set; }

        public int ConsumidoAcumulado { get; set; }
    }

    private sealed class SomaAjustes
    {
        public int NoPeriodo { get; set; }

        public int Acumulado { get; set; }
    }
}
