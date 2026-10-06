namespace Acordu.Application.Projetos;

/// <summary>Projetos e progresso (read side §7). Nulo quando o projeto não existe ou não é visível no contexto.</summary>
public interface IConsultasProjeto
{
    Task<ProgressoProjetoDto?> ProgressoAsync(Guid projetoId, CancellationToken cancellationToken = default);

    Task<Common.ResultadoPaginado<ProjetoResumoDto>> ListarAsync(
        Guid? clienteId, Domain.Enums.StatusProjeto? status, Common.Paginacao paginacao, CancellationToken cancellationToken = default);

    Task<ProjetoDto?> DetalharAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Tarefas canceladas ficam fora das contagens; apontado inclui faturáveis e não faturáveis; estimativa é a do projeto
/// ou, sem ela, a soma das estimativas das tarefas não canceladas.
/// </summary>
public sealed record ProgressoProjetoDto(int TarefasConcluidas, int TarefasTotal, int ApontadoMinutos, int? EstimativaMinutos);
