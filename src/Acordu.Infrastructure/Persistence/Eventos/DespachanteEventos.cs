using System.Reflection;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Infrastructure.Persistence.Eventos;

/// <summary>
/// Despacha os eventos pendentes das raízes rastreadas para os <see cref="IManipuladorEvento{TEvento}"/>, em rodadas:
/// eventos gerados por um manipulador são tratados na rodada seguinte. Mais de <see cref="MaximoRodadas"/> rodadas indica
/// ciclo entre manipuladores e aborta a unidade de trabalho.
/// </summary>
public sealed class DespachanteEventos
{
    public const int MaximoRodadas = 5;

    private static readonly MethodInfo Tratar = typeof(DespachanteEventos)
        .GetMethod(nameof(TratarAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly IServiceProvider _servicos;

    public DespachanteEventos(IServiceProvider servicos)
    {
        ArgumentNullException.ThrowIfNull(servicos);
        _servicos = servicos;
    }

    /// <summary>Despacha até não sobrar evento e devolve todos, na ordem, com o tenant da raiz que os emitiu.</summary>
    public async Task<IReadOnlyList<EventoEmitido>> DespacharAsync(DbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var todos = new List<EventoEmitido>();
        for (var rodada = 1; ; rodada++)
        {
            var pendentes = Recolher(db);
            if (pendentes.Count == 0)
                return todos;
            if (rodada > MaximoRodadas)
                throw new InvalidOperationException(
                    $"Eventos ainda pendentes após {MaximoRodadas} rodadas: provável ciclo entre manipuladores ({string.Join(", ", pendentes.Select(p => p.Evento.GetType().Name).Distinct())}).");

            foreach (var emitido in pendentes)
            {
                var tarefa = (Task)Tratar.MakeGenericMethod(emitido.Evento.GetType())
                    .Invoke(null, [_servicos, emitido.Evento, cancellationToken])!;
                await tarefa;
            }

            todos.AddRange(pendentes);
        }
    }

    private static List<EventoEmitido> Recolher(DbContext db)
    {
        var pendentes = new List<EventoEmitido>();
        foreach (var raiz in db.ChangeTracker.Entries<EntidadeBase>().Select(e => e.Entity).Where(r => r.Eventos.Count > 0).ToList())
        {
            pendentes.AddRange(raiz.Eventos.Select(e => new EventoEmitido(e, raiz.TenantId)));
            raiz.LimparEventos();
        }

        return pendentes;
    }

    private static async Task TratarAsync<TEvento>(IServiceProvider servicos, TEvento evento, CancellationToken cancellationToken)
        where TEvento : IEventoDominio
    {
        foreach (var manipulador in servicos.GetServices<IManipuladorEvento<TEvento>>())
            await manipulador.TratarAsync(evento, cancellationToken);
    }
}

/// <summary>Evento recolhido de uma raiz, com o tenant dela (vai para a outbox).</summary>
public sealed record EventoEmitido(IEventoDominio Evento, Guid TenantId);
