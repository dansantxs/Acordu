using System.Linq.Expressions;
using System.Reflection;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acordu.Infrastructure.Persistence.Cadastros;

/// <summary>
/// Expurgo de cadastro abandonado (RF-AUT-07, F9d), no job diário: empresa não liberada há <see cref="Prazo"/> ou mais
/// desde <see cref="Tenant.CriadoEm"/> e sem dado (<see cref="ClassificacaoDoExpurgo"/>) é apagada inteira, com o
/// criador se ele não tiver vínculo com outra empresa. Cada empresa num escopo e numa transação próprios; a falha de uma
/// é logada e não para as demais. Ponto revisado de IgnoreQueryFilters e de remoção fora da guarda de gravação
/// (convenções §1): é o sistema agindo, sem contexto de empresa.
/// </summary>
public sealed partial class ExpurgoCadastros
{
    public static readonly TimeSpan Prazo = TimeSpan.FromDays(7);

    private static readonly string[] Filtros = [FiltrosGlobais.Tenant, FiltrosGlobais.Cliente];
    private static readonly MethodInfo Existe = typeof(ExpurgoCadastros).GetMethod(nameof(ExisteAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo Apagar = typeof(ExpurgoCadastros).GetMethod(nameof(ApagarAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly IServiceScopeFactory _escopos;
    private readonly TimeProvider _relogio;
    private readonly ILogger<ExpurgoCadastros> _logger;

    public ExpurgoCadastros(IServiceScopeFactory escopos, TimeProvider relogio, ILogger<ExpurgoCadastros> logger)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(logger);
        _escopos = escopos;
        _relogio = relogio;
        _logger = logger;
    }

    /// <summary>Quantas empresas removeu e em quantas falhou.</summary>
    public async Task<(int Removidos, int Falhas)> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var limite = _relogio.GetUtcNow() - Prazo;
        List<Guid> candidatas;
        await using (var escopo = _escopos.CreateAsyncScope())
        {
            candidatas = await escopo.ServiceProvider.GetRequiredService<AcorduDbContext>().Tenants
                .IgnoreQueryFilters(Filtros)
                .Where(t => t.EnvioLiberadoEm == null && t.CriadoEm <= limite)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);
        }

        var removidos = 0;
        var falhas = 0;
        foreach (var tenantId in candidatas)
        {
            try
            {
                if (await ExpurgarAsync(tenantId, limite, cancellationToken))
                    removidos++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                falhas++;
                ExpurgoFalhou(_logger, ex, tenantId);
            }
        }

        return (removidos, falhas);
    }

    private async Task<bool> ExpurgarAsync(Guid tenantId, DateTimeOffset limite, CancellationToken cancellationToken)
    {
        await using var escopo = _escopos.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AcorduDbContext>();
        await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

        // Confere de novo dentro da transação: a empresa pode ter sido liberada desde a seleção.
        var tenant = await db.Tenants.IgnoreQueryFilters(Filtros)
            .SingleOrDefaultAsync(t => t.Id == tenantId && t.EnvioLiberadoEm == null && t.CriadoEm <= limite, cancellationToken);
        if (tenant is null || await TemDadosAsync(db, tenant, cancellationToken))
            return false;

        foreach (var tipo in TiposComTenant(db))
            await (Task<int>)Apagar.MakeGenericMethod(tipo).Invoke(null, [db, tenantId, cancellationToken])!;
        await db.Tenants.IgnoreQueryFilters(Filtros).Where(t => t.Id == tenantId).ExecuteDeleteAsync(cancellationToken);

        if (tenant.CriadoPorUsuarioId is { } criador
            && !await db.Vinculos.IgnoreQueryFilters(Filtros).AnyAsync(v => v.UsuarioId == criador, cancellationToken))
        {
            await db.Set<RefreshToken>().Where(r => r.UsuarioId == criador).ExecuteDeleteAsync(cancellationToken);
            await db.Set<TokenConta>().Where(t => t.UsuarioId == criador).ExecuteDeleteAsync(cancellationToken);
            // Claims, logins e tokens do Identity saem pela FK em cascata.
            await db.Users.Where(u => u.Id == criador).ExecuteDeleteAsync(cancellationToken);
        }

        await transacao.CommitAsync(cancellationToken);
        CadastroExpurgado(_logger, tenantId);
        return true;
    }

    /// <summary>Algum registro de negócio, ou vínculo de outra pessoa que não o criador.</summary>
    private static async Task<bool> TemDadosAsync(AcorduDbContext db, Tenant tenant, CancellationToken cancellationToken)
    {
        foreach (var tipo in ClassificacaoDoExpurgo.Dados)
        {
            if (await (Task<bool>)Existe.MakeGenericMethod(tipo).Invoke(null, [db, tenant.Id, cancellationToken])!)
                return true;
        }

        return await db.Vinculos.IgnoreQueryFilters(Filtros)
            .AnyAsync(v => v.TenantId == tenant.Id && v.UsuarioId != tenant.CriadoPorUsuarioId, cancellationToken);
    }

    /// <summary>Entidades com <c>TenantId</c> (as próprias, não as owned, que saem em cascata com a dona).</summary>
    public static IEnumerable<Type> TiposComTenant(DbContext db) => db.Model.GetEntityTypes()
        .Where(e => !e.IsOwned() && e.FindProperty("TenantId") is not null)
        .Select(e => e.ClrType);

    private static Task<bool> ExisteAsync<T>(AcorduDbContext db, Guid tenantId, CancellationToken cancellationToken)
        where T : class =>
        db.Set<T>().IgnoreQueryFilters(Filtros).AnyAsync(DoTenant<T>(db, tenantId), cancellationToken);

    private static Task<int> ApagarAsync<T>(AcorduDbContext db, Guid tenantId, CancellationToken cancellationToken)
        where T : class =>
        db.Set<T>().IgnoreQueryFilters(Filtros).Where(DoTenant<T>(db, tenantId)).ExecuteDeleteAsync(cancellationToken);

    /// <summary><c>e =&gt; EF.Property&lt;tipo do TenantId&gt;(e, "TenantId") == tenantId</c>; o tipo pode ser anulável.</summary>
    private static Expression<Func<T, bool>> DoTenant<T>(AcorduDbContext db, Guid tenantId)
        where T : class
    {
        var tipo = db.Model.FindEntityType(typeof(T))!.FindProperty("TenantId")!.ClrType;
        var e = Expression.Parameter(typeof(T), "e");
        var propriedade = Expression.Call(typeof(EF), nameof(EF.Property), [tipo], e, Expression.Constant("TenantId"));
        return Expression.Lambda<Func<T, bool>>(Expression.Equal(propriedade, Expression.Constant(tenantId, tipo)), e);
    }

    [LoggerMessage(EventId = 202, Level = LogLevel.Information, Message = "Cadastro abandonado expurgado: empresa {TenantId}.")]
    private static partial void CadastroExpurgado(ILogger logger, Guid tenantId);

    [LoggerMessage(EventId = 203, Level = LogLevel.Error, Message = "Expurgo do cadastro da empresa {TenantId} falhou.")]
    private static partial void ExpurgoFalhou(ILogger logger, Exception erro, Guid tenantId);
}
