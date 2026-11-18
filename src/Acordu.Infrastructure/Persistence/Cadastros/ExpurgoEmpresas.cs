using System.Data;
using Acordu.Application.Empresas;
using Acordu.Domain.Enums;
using Acordu.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acordu.Infrastructure.Persistence.Cadastros;

/// <summary>
/// Passo <c>expurgo-empresas</c> do job diário (RF-EMP-03, F14i; design do F14, D6): empresa VENCIDA ou CANCELADA com o
/// início do só leitura há <see cref="OpcoesEmpresas.PrazoAteExpurgo"/> ou mais é apagada inteira pelo
/// <see cref="ApagadorDeEmpresa"/>, com as contas que só tinham presença nela. Inclui as anteriores ao F14, que a
/// migration deixou CANCELADA com data antiga (F14-112). TESTE e ATIVA (e a demonstração, que é ATIVA) nunca entram
/// (F14-31). O registro do documento (<c>documentos_teste</c>) não tem referência à empresa e fica (F14-37).
/// <para>
/// Cada empresa num escopo e numa transação serializável, com a condição repetida no <c>DELETE</c> da empresa: uma
/// reativação concorrente faz o commit falhar ou o <c>DELETE</c> não achar a linha. A falha de uma é logada, conta em
/// <c>falhas</c> e não para as demais (F14-36). Ponto revisado de <c>IgnoreQueryFilters</c> e de remoção fora das guardas
/// (convenções §1, ADR 0015 item 3), sem contexto de empresa.
/// </para>
/// </summary>
public sealed partial class ExpurgoEmpresas
{
    private static readonly string[] Filtros = [FiltrosGlobais.Tenant, FiltrosGlobais.Cliente];

    private readonly IServiceScopeFactory _escopos;
    private readonly ApagadorDeEmpresa _apagador;
    private readonly OpcoesEmpresas _opcoes;
    private readonly TimeProvider _relogio;
    private readonly ILogger<ExpurgoEmpresas> _logger;

    public ExpurgoEmpresas(
        IServiceScopeFactory escopos,
        ApagadorDeEmpresa apagador,
        OpcoesEmpresas opcoes,
        TimeProvider relogio,
        ILogger<ExpurgoEmpresas> logger)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(apagador);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(logger);
        _escopos = escopos;
        _apagador = apagador;
        _opcoes = opcoes;
        _relogio = relogio;
        _logger = logger;
    }

    /// <summary>Quantas empresas expurgou e em quantas falhou.</summary>
    public async Task<(int Expurgadas, int Falhas)> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var agora = _relogio.GetUtcNow();
        var limite = agora - _opcoes.PrazoAteExpurgo;
        List<Guid> candidatas;
        await using (var escopo = _escopos.CreateAsyncScope())
        {
            candidatas = await Expurgaveis(escopo.ServiceProvider.GetRequiredService<AcorduDbContext>(), limite)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);
        }

        var expurgadas = 0;
        var falhas = 0;
        foreach (var tenantId in candidatas)
        {
            try
            {
                if (await ExpurgarAsync(tenantId, agora, limite, cancellationToken))
                    expurgadas++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                falhas++;
                ExpurgoFalhou(_logger, ex, tenantId);
            }
        }

        return (expurgadas, falhas);
    }

    private async Task<bool> ExpurgarAsync(Guid tenantId, DateTimeOffset agora, DateTimeOffset limite, CancellationToken cancellationToken)
    {
        await using var escopo = _escopos.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AcorduDbContext>();
        await using var transacao = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var tenant = await Expurgaveis(db, limite).AsNoTracking().SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null || !tenant.ExpurgoDevido(agora, _opcoes.PrazoAteExpurgo))
            return false;

        // A empresa sai primeiro, com a condição repetida: reativada ou prorrogada no meio do caminho, nada é apagado.
        var removida = await Expurgaveis(db, limite).Where(t => t.Id == tenantId).ExecuteDeleteAsync(cancellationToken);
        if (removida != 1)
            return false;
        var arquivos = await _apagador.ApagarDadosAsync(db, tenant, cancellationToken);

        await transacao.CommitAsync(cancellationToken);
        EmpresaExpurgada(_logger, tenantId);
        await _apagador.RemoverArquivosAsync(escopo.ServiceProvider, tenantId, arquivos, cancellationToken);
        return true;
    }

    /// <summary>Só leitura desde antes do limite (a mesma regra de <c>Tenant.ExpurgoDevido</c>, traduzida para SQL).</summary>
    private static IQueryable<Domain.Entities.Tenant> Expurgaveis(AcorduDbContext db, DateTimeOffset limite) =>
        db.Tenants.IgnoreQueryFilters(Filtros)
            .Where(t => SituacaoEmpresaExtensions.SituacoesSomenteLeitura.Contains(t.Situacao)
                && t.SomenteLeituraDesde != null
                && t.SomenteLeituraDesde <= limite);

    [LoggerMessage(EventId = 206, Level = LogLevel.Information, Message = "Empresa só leitura expurgada: {TenantId}.")]
    private static partial void EmpresaExpurgada(ILogger logger, Guid tenantId);

    [LoggerMessage(EventId = 207, Level = LogLevel.Error, Message = "Expurgo da empresa só leitura {TenantId} falhou.")]
    private static partial void ExpurgoFalhou(ILogger logger, Exception erro, Guid tenantId);
}
