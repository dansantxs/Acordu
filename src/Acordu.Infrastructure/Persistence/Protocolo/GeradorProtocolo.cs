using System.Data.Common;
using Acordu.Application.Abstractions;
using Acordu.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Acordu.Infrastructure.Persistence.Protocolo;

/// <summary>
/// Protocolo por <c>INSERT ... ON CONFLICT DO UPDATE ... RETURNING</c> na transação da unidade de trabalho (abre uma se
/// ainda não houver; quem faz o commit é o <see cref="UnitOfWork"/>). O lock da linha do contador serializa criações
/// concorrentes do mesmo tenant até o commit; um rollback devolve o número.
/// </summary>
public sealed class GeradorProtocolo : IGeradorProtocolo
{
    private const string Solicitacao = "solicitacao";

    private const string Sql = """
        INSERT INTO contadores (tenant_id, nome, valor) VALUES (@tenant, @nome, 1)
        ON CONFLICT (tenant_id, nome) DO UPDATE SET valor = contadores.valor + 1
        RETURNING valor
        """;

    private readonly AcorduDbContext _db;
    private readonly ITenantContext _contexto;

    public GeradorProtocolo(AcorduDbContext db, ITenantContext contexto)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(contexto);
        _db = db;
        _contexto = contexto;
    }

    public async Task<int> ProximoNumeroSolicitacaoAsync(CancellationToken cancellationToken = default)
    {
        var tenant = _contexto.TenantId
            ?? throw new IsolamentoVioladoException("Gerar protocolo exige tenant no contexto.");
        var transacao = _db.Database.CurrentTransaction ?? await _db.Database.BeginTransactionAsync(cancellationToken);

        await using var comando = _db.Database.GetDbConnection().CreateCommand();
        comando.Transaction = transacao.GetDbTransaction();
        comando.CommandText = Sql;
        Parametro(comando, "tenant", tenant);
        Parametro(comando, "nome", Solicitacao);
        var valor = await comando.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(valor, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void Parametro(DbCommand comando, string nome, object valor)
    {
        var parametro = comando.CreateParameter();
        parametro.ParameterName = nome;
        parametro.Value = valor;
        comando.Parameters.Add(parametro);
    }
}
