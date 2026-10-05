namespace Acordu.Application.Projetos;

/// <summary>Progresso de projeto (read side §7). Nulo quando o projeto não existe ou não é visível no contexto.</summary>
public interface IConsultasProjeto
{
    Task<ProgressoProjetoDto?> ProgressoAsync(Guid projetoId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Tarefas canceladas ficam fora das contagens; apontado inclui faturáveis e não faturáveis; estimativa é a do projeto
/// ou, sem ela, a soma das estimativas das tarefas não canceladas.
/// </summary>
public sealed record ProgressoProjetoDto(int TarefasConcluidas, int TarefasTotal, int ApontadoMinutos, int? EstimativaMinutos);
