using System.Linq.Expressions;
using System.Reflection;
using Acordu.Application.Abstractions;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acordu.Infrastructure.Persistence.Cadastros;

/// <summary>
/// Apaga tudo de uma empresa (design do F14, D6), dentro da transação de quem chama: o expurgo do cadastro abandonado
/// (RF-AUT-07, F9d), o da empresa vencida ou cancelada (RF-EMP-03, F14i) e a restauração da demonstração (F14s,
/// <see cref="ApagarParaRestaurarAsync"/>). No expurgo, quem chama remove antes a própria linha do <see cref="Tenant"/>,
/// com a condição dele repetida no <c>DELETE</c>, e só chama isto se a linha saiu.
/// <para>
/// Ponto revisado de <c>IgnoreQueryFilters</c> e de remoção por <c>ExecuteDelete</c>, fora das guardas de isolamento e
/// de só leitura (convenções §1, ADR 0015 item 3): é o sistema agindo, sem contexto de empresa.
/// </para>
/// </summary>
public sealed partial class ApagadorDeEmpresa
{
    private static readonly string[] Filtros = [FiltrosGlobais.Tenant, FiltrosGlobais.Cliente];
    private static readonly MethodInfo Apagar = typeof(ApagadorDeEmpresa).GetMethod(nameof(ApagarAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly ILogger<ApagadorDeEmpresa> _logger;

    public ApagadorDeEmpresa(ILogger<ApagadorDeEmpresa> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Apaga as entidades com <c>TenantId</c> da empresa (as owned, como anexos e contatos, saem em cascata), tira das
    /// sugestões a empresa e a pessoa e apaga as contas que só tinham presença nela. Devolve as chaves do storage (anexos e
    /// logotipo), lidas antes de apagar, para <see cref="RemoverArquivosAsync"/> depois do commit.
    /// </summary>
    public async Task<IReadOnlyList<string>> ApagarDadosAsync(AcorduDbContext db, Tenant tenant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(tenant);
        var tenantId = tenant.Id;

        var arquivos = await ChavesDosArquivosAsync(db, tenant, cancellationToken);
        var contas = await ContasDaEmpresaAsync(db, tenant, cancellationToken);

        foreach (var tipo in TiposComTenant(db))
            await (Task<int>)Apagar.MakeGenericMethod(tipo).Invoke(null, [db, tenantId, cancellationToken])!;
        // Sugestões são da Nebrys, não do tenant: ficam, mas sem a empresa nem a pessoa (RNF-PRIV-01, F13e, F14-38).
        await db.Set<Sugestao>().Where(sugestao => sugestao.EmpresaId == tenantId)
            .ExecuteUpdateAsync(
                campos => campos.SetProperty(x => x.EmpresaId, (Guid?)null).SetProperty(x => x.PessoaId, (Guid?)null),
                cancellationToken);

        // Depois de apagar o que era desta empresa, o que sobrou de vínculo, sessão ou contato é de outra (F14-32).
        var apagadas = 0;
        foreach (var conta in contas)
        {
            if (await TemRastroForaAsync(db, conta, tenantId, cancellationToken))
                continue;
            apagadas++;
            await db.Set<RefreshToken>().Where(r => r.UsuarioId == conta).ExecuteDeleteAsync(cancellationToken);
            await db.Set<TokenConta>().Where(t => t.UsuarioId == conta).ExecuteDeleteAsync(cancellationToken);
            // Claims, logins e tokens do Identity saem pela FK em cascata.
            await db.Users.Where(u => u.Id == conta).ExecuteDeleteAsync(cancellationToken);
        }

        ContasApagadas(_logger, apagadas, contas.Count - apagadas, tenantId);

        return arquivos;
    }

    /// <summary>
    /// A restauração da empresa de demonstração (F14-88; design do F14, D13): apaga tudo o que tem o <c>TenantId</c> dela,
    /// inclusive vínculos, papéis e calendário, que quem chama semeia de novo, mas fica a linha do <see cref="Tenant"/>,
    /// ficam as contas (as fictícias e as das pessoas indicadas, que têm a própria empresa) e ficam as sessões abertas nela
    /// (os refresh tokens: quem estava na demonstração continua, com o vínculo novo). As sugestões ficam como estão: são da
    /// Nebrys. Devolve as chaves do storage (anexos e logotipo), para <see cref="RemoverArquivosAsync"/> depois do commit.
    /// </summary>
    public static async Task<IReadOnlyList<string>> ApagarParaRestaurarAsync(
        AcorduDbContext db, Tenant tenant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(tenant);
        var arquivos = await ChavesDosArquivosAsync(db, tenant, cancellationToken);
        foreach (var tipo in TiposComTenant(db).Where(t => t != typeof(RefreshToken)))
            await (Task<int>)Apagar.MakeGenericMethod(tipo).Invoke(null, [db, tenant.Id, cancellationToken])!;
        return arquivos;
    }

    /// <summary>
    /// Remove do storage os arquivos da empresa apagada, depois do commit. Melhor esforço: a falha de um fica no log e não
    /// desfaz o expurgo nem impede os demais.
    /// </summary>
    public async Task RemoverArquivosAsync(
        IServiceProvider servicos, Guid tenantId, IReadOnlyList<string> chaves, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(servicos);
        ArgumentNullException.ThrowIfNull(chaves);
        if (chaves.Count == 0 || servicos.GetService<IArmazenamentoArquivos>() is not { } armazenamento)
            return;

        var ficaram = 0;
        foreach (var chave in chaves)
        {
            try
            {
                await armazenamento.RemoverAsync(chave, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                ficaram++;
                ArquivoFicou(_logger, ex, tenantId);
            }
        }

        if (ficaram > 0)
            ArquivosFicaram(_logger, ficaram, tenantId);
    }

    /// <summary>Entidades com <c>TenantId</c> (as próprias, não as owned, que saem em cascata com a dona).</summary>
    public static IEnumerable<Type> TiposComTenant(DbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        return db.Model.GetEntityTypes()
            .Where(e => !e.IsOwned() && e.FindProperty("TenantId") is not null)
            .Select(e => e.ClrType);
    }

    /// <summary>Anexos das interações e dos comentários de tarefa, e o logotipo do portal.</summary>
    private static async Task<IReadOnlyList<string>> ChavesDosArquivosAsync(
        AcorduDbContext db, Tenant tenant, CancellationToken cancellationToken)
    {
        var tenantId = tenant.Id;
        var chaves = await db.Interacoes.IgnoreQueryFilters(Filtros).AsNoTracking()
            .Where(i => i.TenantId == tenantId)
            .SelectMany(i => i.Anexos.Select(a => a.ChaveArmazenamento))
            .ToListAsync(cancellationToken);
        chaves.AddRange(await db.ComentariosTarefa.IgnoreQueryFilters(Filtros).AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .SelectMany(c => c.Anexos.Select(a => a.ChaveArmazenamento))
            .ToListAsync(cancellationToken));
        if (tenant.LogoChave is { } logo)
            chaves.Add(logo);
        return chaves.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>Quem tem conta ligada à empresa: o criador, cada vínculo (equipe e portal) e os contatos com conta.</summary>
    private static async Task<IReadOnlyList<Guid>> ContasDaEmpresaAsync(
        AcorduDbContext db, Tenant tenant, CancellationToken cancellationToken)
    {
        var tenantId = tenant.Id;
        var contas = await db.Vinculos.IgnoreQueryFilters(Filtros).AsNoTracking()
            .Where(v => v.TenantId == tenantId)
            .Select(v => v.UsuarioId)
            .ToListAsync(cancellationToken);
        contas.AddRange(await db.Clientes.IgnoreQueryFilters(Filtros).AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .SelectMany(c => c.Contatos.Where(x => x.UsuarioId != null).Select(x => x.UsuarioId!.Value))
            .ToListAsync(cancellationToken));
        if (tenant.CriadoPorUsuarioId is { } criador)
            contas.Add(criador);
        return contas.Distinct().ToList();
    }

    /// <summary>
    /// A conta fica se a pessoa tem ou já teve presença em outra empresa: vínculo, sessão aberta lá (refresh token, que
    /// fica guardado mesmo revogado), conta ligada a um contato ou criadora de outra empresa (o criador é quem libera o
    /// envio dela, RF-AUT-06). Apagá-la deixaria autoria, contato e criador apontando para o vazio.
    /// Chamado depois de apagar a empresa: o que sobra é de outra.
    /// </summary>
    private static async Task<bool> TemRastroForaAsync(
        AcorduDbContext db, Guid usuarioId, Guid tenantId, CancellationToken cancellationToken) =>
        await db.Vinculos.IgnoreQueryFilters(Filtros).AnyAsync(v => v.UsuarioId == usuarioId && v.TenantId != tenantId, cancellationToken)
        || await db.Set<RefreshToken>().AnyAsync(r => r.UsuarioId == usuarioId && r.TenantId != tenantId, cancellationToken)
        || await db.Clientes.IgnoreQueryFilters(Filtros)
            .AnyAsync(c => c.TenantId != tenantId && c.Contatos.Any(x => x.UsuarioId == usuarioId), cancellationToken)
        || await db.Tenants.IgnoreQueryFilters(Filtros).AnyAsync(t => t.CriadoPorUsuarioId == usuarioId && t.Id != tenantId, cancellationToken);

    private static Task<int> ApagarAsync<T>(AcorduDbContext db, Guid tenantId, CancellationToken cancellationToken)
        where T : class =>
        db.Set<T>().IgnoreQueryFilters(Filtros).Where(DoTenant<T>(db, tenantId)).ExecuteDeleteAsync(cancellationToken);

    /// <summary>
    /// <c>e =&gt; EF.Property&lt;tipo do TenantId&gt;(e, "TenantId") == tenantId</c>; o tipo pode ser anulável. O valor vem de
    /// um membro (como numa closure), para o EF parametrizar em vez de gerar uma consulta por empresa.
    /// </summary>
    internal static Expression<Func<T, bool>> DoTenant<T>(AcorduDbContext db, Guid tenantId)
        where T : class
    {
        var tipo = db.Model.FindEntityType(typeof(T))!.FindProperty("TenantId")!.ClrType;
        var e = Expression.Parameter(typeof(T), "e");
        var propriedade = Expression.Call(typeof(EF), nameof(EF.Property), [tipo], e, Expression.Constant("TenantId"));
        var valor = Expression.Convert(
            Expression.Property(Expression.Constant(new Parametro(tenantId)), nameof(Parametro.Valor)), tipo);
        return Expression.Lambda<Func<T, bool>>(Expression.Equal(propriedade, valor), e);
    }

    private sealed class Parametro(Guid valor)
    {
        public Guid Valor { get; } = valor;
    }

    [LoggerMessage(EventId = 208, Level = LogLevel.Information, Message = "Empresa {TenantId} apagada: {Apagadas} conta(s) apagada(s), {Mantidas} mantida(s) por presença em outra empresa.")]
    private static partial void ContasApagadas(ILogger logger, int apagadas, int mantidas, Guid tenantId);

    [LoggerMessage(EventId = 204, Level = LogLevel.Warning, Message = "Um arquivo da empresa expurgada {TenantId} ficou no storage.")]
    private static partial void ArquivoFicou(ILogger logger, Exception erro, Guid tenantId);

    [LoggerMessage(EventId = 205, Level = LogLevel.Warning, Message = "{Quantidade} arquivo(s) da empresa expurgada {TenantId} ficaram no storage.")]
    private static partial void ArquivosFicaram(ILogger logger, int quantidade, Guid tenantId);
}
