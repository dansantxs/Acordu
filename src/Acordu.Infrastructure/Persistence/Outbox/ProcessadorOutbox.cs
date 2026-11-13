using System.Reflection;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acordu.Infrastructure.Persistence.Outbox;

/// <summary>
/// Entrega as mensagens pendentes da outbox aos <see cref="IManipuladorPosCommit{TEvento}"/>, cada uma num escopo próprio
/// com o contexto fixado no tenant da mensagem. Entrega "pelo menos uma vez"; falha agenda nova tentativa e, esgotadas
/// as tentativas, a mensagem sai da fila com o erro guardado. Chamado no fim da requisição (só as mensagens dela) e pelo
/// job <c>tick</c> (o que sobrou). As mensagens do lote ficam travadas (<c>FOR UPDATE SKIP LOCKED</c>) até o fim dele:
/// dois processamentos ao mesmo tempo (o fim de uma requisição e o tick, ou duas instâncias) nunca pegam a mesma.
/// Mensagem de empresa só leitura é descartada sem entregar, com contagem no log (ADR 0015, item 5; F14-22).
/// </summary>
public sealed partial class ProcessadorOutbox
{
    /// <summary>Motivo gravado em <see cref="MensagemOutbox.Erro"/> da mensagem descartada.</summary>
    public const string MotivoSomenteLeitura = "descartada: empresa só leitura";

    private static readonly MethodInfo Entregar = typeof(ProcessadorOutbox)
        .GetMethod(nameof(EntregarAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly IServiceScopeFactory _escopos;
    private readonly TimeProvider _relogio;
    private readonly ILogger<ProcessadorOutbox> _logger;

    public ProcessadorOutbox(IServiceScopeFactory escopos, TimeProvider relogio, ILogger<ProcessadorOutbox> logger)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(logger);
        _escopos = escopos;
        _relogio = relogio;
        _logger = logger;
    }

    /// <summary>Processa até <paramref name="tamanhoLote"/> mensagens vencidas, das mais antigas; devolve quantas tratou.</summary>
    public async Task<int> ProcessarLoteAsync(int tamanhoLote, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tamanhoLote);
        var agora = _relogio.GetUtcNow();
        return await ProcessarAsync(
            fila => fila.Where(m => m.ProximaTentativaEm <= agora).OrderBy(m => m.Sequencia).Take(tamanhoLote),
            cancellationToken);
    }

    /// <summary>
    /// Processa as mensagens indicadas que ainda estejam pendentes (despacho no fim da requisição que as gravou),
    /// sem esperar o horário da próxima tentativa.
    /// </summary>
    public async Task<int> ProcessarAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
            return 0;

        return await ProcessarAsync(fila => fila.Where(m => ids.Contains(m.Id)).OrderBy(m => m.Sequencia), cancellationToken);
    }

    private async Task<int> ProcessarAsync(
        Func<IQueryable<MensagemOutbox>, IQueryable<MensagemOutbox>> selecionar, CancellationToken cancellationToken)
    {
        await using var escopoFila = _escopos.CreateAsyncScope();
        var db = escopoFila.ServiceProvider.GetRequiredService<AcorduDbContext>();
        var pendentes = db.Set<MensagemOutbox>().Where(m => m.ProcessadaEm == null && m.FalhouEm == null);
        var candidatas = await selecionar(pendentes).Select(m => m.Id).ToArrayAsync(cancellationToken);
        if (candidatas.Length == 0)
            return 0;

        // Trava as candidatas ainda pendentes; as que outro processamento já travou ficam de fora (SKIP LOCKED).
        await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);
        var mensagens = await db.Set<MensagemOutbox>()
            .FromSql($"""
                SELECT * FROM outbox_mensagens
                WHERE id = ANY({candidatas}) AND processada_em IS NULL AND falhou_em IS NULL
                ORDER BY sequencia
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        var tenants = mensagens.Select(m => m.TenantId).Distinct().ToArray();
        // Ponto revisado de IgnoreQueryFilters: a fila não tem tenant no contexto (ADR 0015, item 5).
        var soLeitura = (await db.Tenants.IgnoreQueryFilters([FiltrosGlobais.Tenant])
            .Where(t => tenants.Contains(t.Id) && SituacaoEmpresaExtensions.SituacoesSomenteLeitura.Contains(t.Situacao))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken)).ToHashSet();
        var descartadas = 0;

        foreach (var mensagem in mensagens)
        {
            if (soLeitura.Contains(mensagem.TenantId))
            {
                // Só eventos de domínio do tenant passam pela outbox; e-mails da conta e avisos de situação aos Donos
                // saem fora dela e não são tocados aqui.
                mensagem.Descartar(MotivoSomenteLeitura, _relogio.GetUtcNow());
                descartadas++;
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }

            try
            {
                await EntregarNoTenantAsync(mensagem, cancellationToken);
                mensagem.MarcarProcessada(_relogio.GetUtcNow());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                mensagem.RegistrarFalha($"{ex.GetType().Name}: {ex.Message}", _relogio.GetUtcNow());
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        await transacao.CommitAsync(cancellationToken);
        if (descartadas > 0)
            MensagensDescartadas(_logger, descartadas);
        return mensagens.Count;
    }

    [LoggerMessage(EventId = 300, Level = LogLevel.Information, Message = "Outbox: {Quantidade} mensagem(ns) de empresa só leitura descartada(s).")]
    private static partial void MensagensDescartadas(ILogger logger, int quantidade);

    private async Task EntregarNoTenantAsync(MensagemOutbox mensagem, CancellationToken cancellationToken)
    {
        var evento = SerializadorEventos.Desserializar(mensagem.Tipo, mensagem.Conteudo);
        await using var escopo = _escopos.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<ContextoTenant>().DefinirEquipe(mensagem.TenantId);
        escopo.ServiceProvider.GetRequiredService<OrigemDoEvento>().Definir(mensagem.AutorId, mensagem.Id, mensagem.OcorridaEm);

        var tarefa = (Task)Entregar.MakeGenericMethod(evento.GetType())
            .Invoke(null, [escopo.ServiceProvider, evento, cancellationToken])!;
        await tarefa;
    }

    private static async Task EntregarAsync<TEvento>(IServiceProvider servicos, TEvento evento, CancellationToken cancellationToken)
        where TEvento : IEventoDominio
    {
        foreach (var manipulador in servicos.GetServices<IManipuladorPosCommit<TEvento>>())
            await manipulador.TratarAsync(evento, cancellationToken);
    }
}
