using Acordu.Application.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Agenda;

/// <summary>
/// Linhas da agenda pelos filtros do contexto (read side §6a). As regras de situação e a expansão das recorrências ficam
/// em <see cref="LeituraAgenda"/>; aqui só se filtra e se resolve nomes.
/// </summary>
public interface IConsultasAgenda
{
    /// <summary>Equipe do tenant (vínculos sem cliente), por nome.</summary>
    Task<IReadOnlyList<Referencia>> EquipeAsync(CancellationToken cancellationToken = default);

    /// <summary>Em aberto, com SLA e prazo de solução antes de <paramref name="antes"/> (inclui as atrasadas).</summary>
    Task<IReadOnlyList<SolicitacaoAgenda>> SolicitacoesAsync(
        DateTimeOffset antes, FiltroAgenda filtro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Não canceladas com dia (planejado ou, sem ele, o prazo) no período; com <paramref name="atrasadas"/>, também as em
    /// aberto com dia antes do período.
    /// </summary>
    Task<IReadOnlyList<TarefaAgenda>> TarefasAsync(
        DateOnly de, DateOnly ate, bool atrasadas, FiltroAgenda filtro, CancellationToken cancellationToken = default);

    /// <summary>Em andamento com início ou prazo, cruzando o período; com <paramref name="atrasados"/>, também os que acabaram antes.</summary>
    Task<IReadOnlyList<ProjetoAgenda>> ProjetosAsync(
        DateOnly de, DateOnly ate, bool atrasados, Guid? clienteId, CancellationToken cancellationToken = default);

    /// <summary>Ativas com próxima data até <paramref name="ate"/>.</summary>
    Task<IReadOnlyList<RecorrenciaAgenda>> RecorrenciasAsync(DateOnly ate, Guid? clienteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ativos que cruzam o período, com o fim da vigência e, para os vigentes hoje, o percentual de consumo do período
    /// corrente (fórmulas do read side §3).
    /// </summary>
    Task<IReadOnlyList<ContratoAgenda>> ContratosAsync(
        DateOnly de, DateOnly ate, DateOnly hoje, Guid? clienteId, CancellationToken cancellationToken = default);
}

public sealed record FiltroAgenda(Guid? ClienteId, Guid? ResponsavelId);

public sealed record SolicitacaoAgenda(
    Guid Id,
    int Numero,
    string Titulo,
    Referencia Cliente,
    Referencia? Responsavel,
    DateTimeOffset PrazoResolucao,
    DateTimeOffset PrazoPrimeiraResposta,
    DateTimeOffset LimiteRisco,
    bool Pausada,
    bool Respondida);

public sealed record TarefaAgenda(
    Guid Id,
    string Titulo,
    StatusTarefa Status,
    Referencia? Cliente,
    Referencia? Responsavel,
    DateOnly? PlanejadaPara,
    DateOnly? Prazo,
    int? EstimativaMinutos,
    Guid? SolicitacaoId,
    Guid? ProjetoId);

public sealed record ProjetoAgenda(Guid Id, string Nome, Referencia Cliente, DateOnly? Inicio, DateOnly? Prazo);

public sealed record RecorrenciaAgenda(
    Guid Id, string Titulo, Referencia Cliente, Referencia? Responsavel, int? EstimativaMinutos, RegraRecorrencia Regra, DateOnly ProximaData);

public sealed record ContratoAgenda(Guid Id, Referencia Cliente, TipoContrato Tipo, DateOnly? FimVigencia, int? Percentual);
