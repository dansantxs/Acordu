using Acordu.Application.Common;

namespace Acordu.Application.Capacidade;

/// <summary>
/// Capacidade da semana (contrato §11c, read side §6b): horas planejadas × capacidade por pessoa da equipe. Planejado =
/// soma das estimativas das tarefas em aberto da pessoa com dia (planejado ou, sem ele, o prazo) na semana.
/// </summary>
/// <param name="SemResponsavel">Tarefas da semana sem ninguém, para a faixa "Sem responsável" do plano.</param>
public sealed record CapacidadeSemanaDto(
    DateOnly Semana,
    DateOnly Ate,
    IReadOnlyList<CapacidadePessoaDto> Pessoas,
    CargaDto SemResponsavel);

/// <param name="CapacidadeMinutos">Capacidade semanal da pessoa (padrão 40h).</param>
/// <param name="Percentual">Planejado sobre a capacidade; nulo com capacidade zero.</param>
public sealed record CapacidadePessoaDto(
    Referencia Pessoa,
    int CapacidadeMinutos,
    int? Percentual,
    CargaDto Carga);

/// <param name="TarefasSemEstimativa">Contam no total de tarefas, mas não nos minutos.</param>
/// <param name="Solicitacoes">Solicitações em aberto com prazo de solução na semana (nulo sem <c>solicitacao.ver</c>): só contagem, não têm estimativa.</param>
/// <param name="PorDia">Minutos planejados em cada dia da semana, de segunda a domingo.</param>
public sealed record CargaDto(
    int PlanejadoMinutos,
    int Tarefas,
    int TarefasSemEstimativa,
    int? Solicitacoes,
    IReadOnlyList<CargaDiaDto> PorDia);

public sealed record CargaDiaDto(DateOnly Data, int PlanejadoMinutos);
