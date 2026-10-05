using System.Reflection;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Domain.Common;
using Acordu.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Infrastructure.Persistence.Outbox;

/// <summary>
/// Entrega as mensagens pendentes da outbox aos <see cref="IManipuladorPosCommit{TEvento}"/>, cada uma num escopo próprio
/// com o contexto fixado no tenant da mensagem. Entrega "pelo menos uma vez"; falha agenda nova tentativa e, esgotadas
/// as tentativas, a mensagem sai da fila com o erro guardado. v1: uma instância processando (sem lock entre réplicas);
/// o HostedService que chama este processador entra com a Api.
/// </summary>
public sealed class ProcessadorOutbox
{
    private static readonly MethodInfo Entregar = typeof(ProcessadorOutbox)
        .GetMethod(nameof(EntregarAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly IServiceScopeFactory _escopos;
    private readonly TimeProvider _relogio;

    public ProcessadorOutbox(IServiceScopeFactory escopos, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(relogio);
        _escopos = escopos;
        _relogio = relogio;
    }

    /// <summary>Processa até <paramref name="tamanhoLote"/> mensagens vencidas, das mais antigas; devolve quantas tratou.</summary>
    public async Task<int> ProcessarLoteAsync(int tamanhoLote, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tamanhoLote);
        await using var escopoFila = _escopos.CreateAsyncScope();
        var db = escopoFila.ServiceProvider.GetRequiredService<AcorduDbContext>();
        var agora = _relogio.GetUtcNow();

        var mensagens = await db.Set<MensagemOutbox>()
            .Where(m => m.ProcessadaEm == null && m.FalhouEm == null && m.ProximaTentativaEm <= agora)
            .OrderBy(m => m.Sequencia)
            .Take(tamanhoLote)
            .ToListAsync(cancellationToken);

        foreach (var mensagem in mensagens)
        {
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

        return mensagens.Count;
    }

    private async Task EntregarNoTenantAsync(MensagemOutbox mensagem, CancellationToken cancellationToken)
    {
        var evento = SerializadorEventos.Desserializar(mensagem.Tipo, mensagem.Conteudo);
        await using var escopo = _escopos.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<ContextoTenant>().DefinirEquipe(mensagem.TenantId);

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
