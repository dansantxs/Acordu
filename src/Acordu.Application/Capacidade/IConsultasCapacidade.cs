using Acordu.Application.Common;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Capacidade;

/// <summary>Linhas da capacidade pelos filtros do contexto (read side §6b); a soma fica em <see cref="LeituraCapacidade"/>.</summary>
public interface IConsultasCapacidade
{
    /// <summary>Equipe (vínculos sem cliente) com a capacidade semanal, por nome.</summary>
    Task<IReadOnlyList<MembroCapacidade>> EquipeAsync(CancellationToken cancellationToken = default);

    /// <summary>Tarefas em aberto com dia (planejado ou, sem ele, o prazo) entre <paramref name="de"/> e <paramref name="ate"/>.</summary>
    Task<IReadOnlyList<TarefaCapacidade>> TarefasAsync(DateOnly de, DateOnly ate, CancellationToken cancellationToken = default);

    /// <summary>Responsáveis das solicitações em aberto com SLA e prazo de solução em [<paramref name="desde"/>, <paramref name="antes"/>).</summary>
    Task<IReadOnlyList<Guid?>> SolicitacoesAsync(DateTimeOffset desde, DateTimeOffset antes, CancellationToken cancellationToken = default);
}

public sealed record MembroCapacidade(Referencia Pessoa, Duracao? Capacidade);

public sealed record TarefaCapacidade(Guid? ResponsavelId, DateOnly Dia, int? EstimativaMinutos);
