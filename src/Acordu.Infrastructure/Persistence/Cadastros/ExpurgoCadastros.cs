using System.Data;
using System.Reflection;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Persistence.Documentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acordu.Infrastructure.Persistence.Cadastros;

/// <summary>
/// Expurgo de cadastro abandonado (RF-AUT-07, F9d), no job diário: empresa não liberada há <see cref="Prazo"/> ou mais
/// desde <see cref="Tenant.CriadoEm"/> e sem dado (<see cref="ClassificacaoDoExpurgo"/>) é apagada inteira pelo
/// <see cref="ApagadorDeEmpresa"/>, com o criador se ele não tiver vínculo com outra empresa, e o registro do documento
/// dela sai (decisão 16 do G2 do F14, F14-104). Cada empresa num escopo e numa transação próprios; a falha de uma é logada
/// e não para as demais. A empresa de demonstração nunca entra (RF-NEB-02, F14s): ela nasce liberada e tem a marca no
/// filtro. Ponto revisado de IgnoreQueryFilters e de remoção fora da guarda de gravação (convenções §1): é o
/// sistema agindo, sem contexto de empresa.
/// </summary>
public sealed partial class ExpurgoCadastros
{
    public static readonly TimeSpan Prazo = TimeSpan.FromDays(7);

    private static readonly string[] Filtros = [FiltrosGlobais.Tenant, FiltrosGlobais.Cliente];
    private static readonly MethodInfo Existe = typeof(ExpurgoCadastros).GetMethod(nameof(ExisteAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly IServiceScopeFactory _escopos;
    private readonly ApagadorDeEmpresa _apagador;
    private readonly TimeProvider _relogio;
    private readonly ILogger<ExpurgoCadastros> _logger;

    public ExpurgoCadastros(
        IServiceScopeFactory escopos, ApagadorDeEmpresa apagador, TimeProvider relogio, ILogger<ExpurgoCadastros> logger)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(apagador);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(logger);
        _escopos = escopos;
        _apagador = apagador;
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
                .Where(t => t.EnvioLiberadoEm == null && !t.Demonstracao && t.CriadoEm <= limite)
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
        // Serializável: uma confirmação, um cadastro ou um aceite de convite concorrente faz o commit falhar (conta em
        // falhas, e o diário seguinte reavalia) em vez de o expurgo apagar sobre um estado que já mudou.
        await using var transacao = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var tenant = await db.Tenants.IgnoreQueryFilters(Filtros).AsNoTracking()
            .SingleOrDefaultAsync(
                t => t.Id == tenantId && t.EnvioLiberadoEm == null && !t.Demonstracao && t.CriadoEm <= limite, cancellationToken);
        if (tenant is null || await TemDadosAsync(db, tenant, cancellationToken))
            return false;

        // A empresa sai primeiro, com a condição repetida: liberada no meio do caminho, nada é apagado.
        var removida = await db.Tenants.IgnoreQueryFilters(Filtros)
            .Where(t => t.Id == tenantId && t.EnvioLiberadoEm == null && !t.Demonstracao)
            .ExecuteDeleteAsync(cancellationToken);
        if (removida != 1)
            return false;
        var arquivos = await _apagador.ApagarDadosAsync(db, tenant, cancellationToken);
        // O teste nunca foi usado: o documento volta a poder abrir um (F14-104). Na mesma transação.
        if (tenant.Documento is { } documento)
        {
            await escopo.ServiceProvider.GetRequiredService<RegistroDocumentos>()
                .DesfazerTesteDoCadastroAsync(documento, tenant.CriadoEm, cancellationToken);
        }

        await transacao.CommitAsync(cancellationToken);
        CadastroExpurgado(_logger, tenantId);
        await _apagador.RemoverArquivosAsync(escopo.ServiceProvider, tenantId, arquivos, cancellationToken);
        return true;
    }

    /// <summary>Algum registro de negócio, papel criado pela empresa ou vínculo de outra pessoa que não o criador.</summary>
    private static async Task<bool> TemDadosAsync(AcorduDbContext db, Tenant tenant, CancellationToken cancellationToken)
    {
        foreach (var tipo in ClassificacaoDoExpurgo.Dados)
        {
            if (await (Task<bool>)Existe.MakeGenericMethod(tipo).Invoke(null, [db, tenant.Id, cancellationToken])!)
                return true;
        }

        return await db.Papeis.IgnoreQueryFilters(Filtros).AnyAsync(p => p.TenantId == tenant.Id && !p.Padrao, cancellationToken)
            || await db.Vinculos.IgnoreQueryFilters(Filtros)
                .AnyAsync(v => v.TenantId == tenant.Id && v.UsuarioId != tenant.CriadoPorUsuarioId, cancellationToken);
    }

    private static Task<bool> ExisteAsync<T>(AcorduDbContext db, Guid tenantId, CancellationToken cancellationToken)
        where T : class =>
        db.Set<T>().IgnoreQueryFilters(Filtros).AnyAsync(ApagadorDeEmpresa.DoTenant<T>(db, tenantId), cancellationToken);

    [LoggerMessage(EventId = 202, Level = LogLevel.Information, Message = "Cadastro abandonado expurgado: empresa {TenantId}.")]
    private static partial void CadastroExpurgado(ILogger logger, Guid tenantId);

    [LoggerMessage(EventId = 203, Level = LogLevel.Error, Message = "Expurgo do cadastro da empresa {TenantId} falhou.")]
    private static partial void ExpurgoFalhou(ILogger logger, Exception erro, Guid tenantId);
}
