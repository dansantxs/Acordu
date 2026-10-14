using Acordu.Domain.Enums;

namespace Acordu.Application.Dashboard;

/// <summary>
/// Tela inicial (contrato §11, read side §6). Bloco nulo: quem pede não tem a permissão de leitura da área (fila e SLA:
/// <c>solicitacao.ver</c>; horas: <c>apontamento.ver</c>; contratos: <c>contrato.ver</c>; tarefas: <c>tarefa.ver</c>).
/// </summary>
public sealed record DashboardDto(
    DateOnly De,
    DateOnly Ate,
    FilaDto? Fila,
    SlaDashboardDto? Sla,
    HorasDashboardDto? Horas,
    IReadOnlyList<ContratoDashboardDto>? Contratos,
    TarefasDashboardDto? Tarefas);

/// <summary>Solicitações não finais por status; <see cref="SemResponsavel"/> entre elas.</summary>
public sealed record FilaDto(int Abertas, int EmAndamento, int AguardandoCliente, int AguardandoTerceiro, int SemResponsavel);

/// <summary>Em aberto agora (em risco, violadas) e o cumprimento das resolvidas no período (nulo sem nenhuma com SLA).</summary>
public sealed record SlaDashboardDto(int EmRisco, int Violadas, int? CumprimentoPeriodo);

public sealed record HorasDashboardDto(int FaturavelMinutos, int NaoFaturavelMinutos, IReadOnlyList<HorasClienteDto> PorCliente);

public sealed record HorasClienteDto(Guid ClienteId, string Nome, int Minutos);

/// <summary>Consumo do período corrente (no banco de horas, o acumulado).</summary>
public sealed record ContratoDashboardDto(
    Guid ContratoId, string Cliente, TipoContrato Tipo, int? FranquiaMinutos, int ConsumidoMinutos, int? Percentual);

public sealed record TarefasDashboardDto(int Vencidas, IReadOnlyList<ProximaRecorrenteDto>? ProximasRecorrentes);

public sealed record ProximaRecorrenteDto(Guid RecorrenciaId, string Titulo, string Cliente, DateOnly Data);
