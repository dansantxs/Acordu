using System.Linq.Expressions;
using System.Reflection;
using Acordu.Application.Abstractions;
using Acordu.Application.Exemplos;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.Cadastros;
using Acordu.Infrastructure.Persistence.Colaboracao;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Exemplos;

/// <summary>
/// Dados de exemplo no tenant do contexto (RF-EMP-05, F14r; design do F14, D13; read side §8b). Exemplo é o cliente com
/// <see cref="Cliente.Exemplo"/> e tudo o que tem o <c>ClienteId</c> dele (o <c>ClienteId</c> desnormalizado do ADR
/// 0002), mais o que pende de uma tarefa dele (cronômetro, comentário) e os rastros que apontam para esses itens
/// (seguidores, marcas dos alertas). Tudo pelos filtros globais do contexto: nada de outro tenant é lido nem apagado.
/// <para>
/// A remoção é ponto revisado de <c>ExecuteDelete</c> fora do change tracker (convenções §1, ADR 0015 item 3): a
/// situação da empresa é conferida antes pelo caso de uso (<see cref="ComandosExemplos"/>), e os clientes saem pela
/// unidade de trabalho, com as duas guardas de gravação, na mesma transação.
/// </para>
/// </summary>
public sealed class DadosDeExemplo : IDadosDeExemplo
{
    private const string ClienteId = "ClienteId";
    private const string TarefaId = "TarefaId";

    /// <summary>
    /// Os tipos de registro de negócio do expurgo (<see cref="ClassificacaoDoExpurgo.Dados"/>) que
    /// <see cref="SituacaoAsync"/> confere, um por consulta; um teste compara as duas listas.
    /// </summary>
    public static readonly IReadOnlySet<Type> TiposConferidos = new HashSet<Type>
    {
        typeof(AcessoPortal),
        typeof(AjusteSaldo),
        typeof(Apontamento),
        typeof(Categoria),
        typeof(Cliente),
        typeof(ComentarioTarefa),
        typeof(Contrato),
        typeof(ConversaPorEmail),
        typeof(ConviteEquipe),
        typeof(ConvitePortal),
        typeof(Cronometro),
        typeof(EmailRecebido),
        typeof(Interacao),
        typeof(Projeto),
        typeof(RecorrenciaTarefa),
        typeof(Solicitacao),
        typeof(Tarefa),
    };

    private static readonly MethodInfo RemoverPorReferencia =
        typeof(DadosDeExemplo).GetMethod(nameof(RemoverPorReferenciaAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly AcorduDbContext _db;
    private readonly ITenantContext _contexto;

    public DadosDeExemplo(AcorduDbContext db, ITenantContext contexto)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(contexto);
        _db = db;
        _contexto = contexto;
    }

    public async Task<SituacaoDosExemplos> SituacaoAsync(Guid? criadorId, CancellationToken cancellationToken = default)
    {
        var exemplos = _db.Clientes.Where(c => c.Exemplo).Select(c => c.Id);
        var tarefasDeExemplo = _db.Tarefas.Where(t => t.ClienteId != null && exemplos.Contains(t.ClienteId.Value)).Select(t => t.Id);
        var carregados = await exemplos.AnyAsync(cancellationToken);

        // Uma consulta por tipo de TiposConferidos, parando no primeiro dado próprio.
        var proprios = await _db.Clientes.AnyAsync(c => !c.Exemplo, cancellationToken)
            || await _db.Categorias.AnyAsync(cancellationToken)
            || await _db.ConvitesEquipe.AnyAsync(cancellationToken)
            || await _db.Contratos.AnyAsync(x => !exemplos.Contains(x.ClienteId), cancellationToken)
            || await _db.AjustesSaldo.AnyAsync(x => !exemplos.Contains(x.ClienteId), cancellationToken)
            || await _db.Solicitacoes.AnyAsync(x => !exemplos.Contains(x.ClienteId), cancellationToken)
            || await _db.Interacoes.AnyAsync(x => !exemplos.Contains(x.ClienteId), cancellationToken)
            || await _db.Projetos.AnyAsync(x => !exemplos.Contains(x.ClienteId), cancellationToken)
            || await _db.Recorrencias.AnyAsync(x => !exemplos.Contains(x.ClienteId), cancellationToken)
            || await _db.AcessosPortal.AnyAsync(x => !exemplos.Contains(x.ClienteId), cancellationToken)
            || await _db.ConversasPorEmail.AnyAsync(x => !exemplos.Contains(x.ClienteId), cancellationToken)
            || await _db.ConvitesPortal.AnyAsync(x => !exemplos.Contains(x.ClienteId), cancellationToken)
            || await _db.Tarefas.AnyAsync(x => x.ClienteId == null || !exemplos.Contains(x.ClienteId.Value), cancellationToken)
            || await _db.Apontamentos.AnyAsync(x => x.ClienteId == null || !exemplos.Contains(x.ClienteId.Value), cancellationToken)
            || await _db.Cronometros.AnyAsync(x => !tarefasDeExemplo.Contains(x.TarefaId), cancellationToken)
            || await _db.ComentariosTarefa.AnyAsync(x => !tarefasDeExemplo.Contains(x.TarefaId), cancellationToken)
            // Um e-mail recebido (F15d) é da empresa em uso, não dos exemplos.
            || await _db.EmailsRecebidos.AnyAsync(cancellationToken)
            // O resto da regra do expurgo (ExpurgoCadastros): papel criado pela empresa, vínculo de outra pessoa.
            || await _db.Papeis.AnyAsync(p => !p.Padrao, cancellationToken)
            || await _db.Vinculos.AnyAsync(
                v => v.UsuarioId != criadorId && (v.ClienteId == null || !exemplos.Contains(v.ClienteId.Value)), cancellationToken);
        return new SituacaoDosExemplos(carregados, proprios);
    }

    public async Task<ExemplosRemovidos> RemoverAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _contexto.TenantId
            ?? throw new InvalidOperationException("Apagar os dados de exemplo exige tenant no contexto.");
        var clientes = await _db.Clientes.Where(c => c.Exemplo).ToListAsync(cancellationToken);
        if (clientes.Count == 0)
            return new ExemplosRemovidos(0, []);

        var ids = clientes.Select(c => c.Id).ToList();
        var tarefas = await _db.Tarefas.Where(t => t.ClienteId != null && ids.Contains(t.ClienteId.Value)).Select(t => t.Id).ToListAsync(cancellationToken);
        var solicitacoes = await _db.Solicitacoes.Where(s => ids.Contains(s.ClienteId)).Select(s => s.Id).ToListAsync(cancellationToken);
        var projetos = await _db.Projetos.Where(p => ids.Contains(p.ClienteId)).Select(p => p.Id).ToListAsync(cancellationToken);
        var contratos = await _db.Contratos.Where(c => ids.Contains(c.ClienteId)).Select(c => c.Id).ToListAsync(cancellationToken);
        var cronometros = await _db.Cronometros.Where(c => tarefas.Contains(c.TarefaId)).Select(c => c.Id).ToListAsync(cancellationToken);
        var arquivos = await _db.Interacoes.Where(i => ids.Contains(i.ClienteId))
            .SelectMany(i => i.Anexos.Select(a => a.ChaveArmazenamento))
            .ToListAsync(cancellationToken);
        arquivos.AddRange(await _db.ComentariosTarefa.Where(c => tarefas.Contains(c.TarefaId))
            .SelectMany(c => c.Anexos.Select(a => a.ChaveArmazenamento))
            .ToListAsync(cancellationToken));

        // Na transação da unidade de trabalho, que grava os clientes e faz o commit (como o GeradorProtocolo).
        if (_db.Database.CurrentTransaction is null)
            await _db.Database.BeginTransactionAsync(cancellationToken);

        var doCliente = new Referencias(ids);
        foreach (var tipo in TiposComCliente(_db))
            await RemoverAsync(tipo, ClienteId, tenantId, doCliente, cancellationToken);
        var daTarefa = new Referencias(tarefas);
        foreach (var tipo in TiposDaTarefa(_db))
            await RemoverAsync(tipo, TarefaId, tenantId, daTarefa, cancellationToken);

        // Rastros sem cliente que apontam para os itens apagados.
        var alvos = solicitacoes.Concat(tarefas).Concat(projetos).ToList();
        await _db.Set<Seguidor>().Where(s => alvos.Contains(s.AlvoId)).ExecuteDeleteAsync(cancellationToken);
        var referencias = alvos.Concat(contratos).Concat(cronometros).ToList();
        await _db.Set<MarcadorAviso>().Where(m => referencias.Contains(m.Referencia)).ExecuteDeleteAsync(cancellationToken);

        // Os clientes (com os contatos) pela unidade de trabalho: as guardas de gravação valem para eles.
        _db.Clientes.RemoveRange(clientes);
        return new ExemplosRemovidos(clientes.Count, arquivos.Distinct(StringComparer.Ordinal).ToList());
    }

    /// <summary>Entidades (não owned) com <c>ClienteId</c>: as raízes ligadas a cliente, o vínculo do contato e o feed.</summary>
    public static IEnumerable<Type> TiposComCliente(DbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        return db.Model.GetEntityTypes()
            .Where(e => !e.IsOwned() && e.FindProperty(ClienteId) is not null)
            .Select(e => e.ClrType);
    }

    /// <summary>Entidades (não owned) que pendem de uma tarefa sem ter o cliente: cronômetro e comentário.</summary>
    public static IEnumerable<Type> TiposDaTarefa(DbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        return db.Model.GetEntityTypes()
            .Where(e => !e.IsOwned() && e.FindProperty(TarefaId) is not null && e.FindProperty(ClienteId) is null)
            .Select(e => e.ClrType);
    }

    private Task<int> RemoverAsync(Type tipo, string propriedade, Guid tenantId, Referencias referencias, CancellationToken cancellationToken) =>
        (Task<int>)RemoverPorReferencia.MakeGenericMethod(tipo).Invoke(null, [_db, propriedade, tenantId, referencias, cancellationToken])!;

    private static Task<int> RemoverPorReferenciaAsync<T>(
        AcorduDbContext db, string propriedade, Guid tenantId, Referencias referencias, CancellationToken cancellationToken)
        where T : class =>
        db.Set<T>()
            .Where(ApagadorDeEmpresa.DoTenant<T>(db, tenantId))
            .Where(Entre<T>(db, propriedade, referencias))
            .ExecuteDeleteAsync(cancellationToken);

    /// <summary>
    /// <c>e =&gt; lista.Contains(EF.Property&lt;tipo&gt;(e, propriedade))</c>, com a lista do tipo da propriedade (que pode
    /// ser anulável), vinda de um membro para o EF parametrizar.
    /// </summary>
    private static Expression<Func<T, bool>> Entre<T>(AcorduDbContext db, string propriedade, Referencias referencias)
        where T : class
    {
        var tipo = db.Model.FindEntityType(typeof(T))!.FindProperty(propriedade)!.ClrType;
        var e = Expression.Parameter(typeof(T), "e");
        var valor = Expression.Call(typeof(EF), nameof(EF.Property), [tipo], e, Expression.Constant(propriedade));
        var lista = Expression.Property(
            Expression.Constant(referencias),
            tipo == typeof(Guid) ? nameof(Referencias.Valores) : nameof(Referencias.Anulaveis));
        var contem = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [tipo], lista, valor);
        return Expression.Lambda<Func<T, bool>>(contem, e);
    }

    private sealed class Referencias(List<Guid> valores)
    {
        public List<Guid> Valores { get; } = valores;

        public List<Guid?> Anulaveis { get; } = valores.ConvertAll(v => (Guid?)v);
    }
}
