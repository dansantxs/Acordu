using Acordu.Application.Abstractions;
using Acordu.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>
/// Único caminho para SQL cru do read side (convenções §1, ADR 0002). SQL cru não passa pelos filtros globais, então o
/// helper entrega o escopo do contexto e confere, antes de executar, que o <c>TenantId</c> — e o <c>ClienteId</c>, no
/// contexto de contato — estão entre os parâmetros da consulta.
/// Padrão para servir equipe e contato com o mesmo texto:
/// <c>AND (CAST({e.ClienteId} AS uuid) IS NULL OR x.cliente_id = {e.ClienteId})</c> (o CAST tipa o parâmetro nulo).
/// Trechos interpolados viram parâmetros, nunca SQL: o texto da consulta é sempre literal.
/// </summary>
public sealed class ConsultaSql
{
    private readonly AcorduDbContext _db;
    private readonly ITenantContext _contexto;

    public ConsultaSql(AcorduDbContext db, ITenantContext contexto)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(contexto);
        _db = db;
        _contexto = contexto;
    }

    /// <summary>Executa a consulta montada com o escopo do contexto. Colunas em snake_case mapeiam as propriedades de <typeparamref name="T"/>.</summary>
    public Task<List<T>> ListarAsync<T>(Func<EscopoConsulta, FormattableString> montar, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(montar);
        var escopo = Escopo();
        var sql = montar(escopo);
        var parametros = sql.GetArguments();
        if (!parametros.Contains(escopo.TenantId))
            throw new IsolamentoVioladoException("Consulta SQL sem o TenantId do contexto entre os parâmetros.");
        if (escopo.ClienteId is { } cliente && !parametros.Contains(cliente))
            throw new IsolamentoVioladoException("Consulta SQL de contato sem o ClienteId do contexto entre os parâmetros.");

        return _db.Database.SqlQuery<T>(sql).ToListAsync(cancellationToken);
    }

    private EscopoConsulta Escopo()
    {
        var tenant = _contexto.TenantId
            ?? throw new IsolamentoVioladoException("Consulta SQL exige tenant no contexto.");
        if (!_contexto.EhCliente)
            return new EscopoConsulta(tenant, null);

        return new EscopoConsulta(tenant, _contexto.ClienteId
            ?? throw new IsolamentoVioladoException("Contexto de contato sem ClienteId."));
    }
}

/// <summary>Tenant do contexto e, no contexto de contato, o cliente — os dois parâmetros obrigatórios do SQL do read side.</summary>
public sealed record EscopoConsulta(Guid TenantId, Guid? ClienteId);
