using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Portal;

// DTOs exclusivos do portal (contrato §12): nada de status interno, Ids de pessoas da equipe, tempo não visível ou
// interação interna. Nunca reaproveitar DTO da equipe aqui (teste de arquitetura).

/// <summary>
/// <c>GET /portal/eu</c>: contato, cliente, prestador e o que o escopo libera. <see cref="SolicitacaoId"/> só na sessão
/// de link (a única solicitação que ela abre).
/// </summary>
public sealed record PortalEuDto(
    ContatoPortalDto Contato,
    ClientePortalDto Cliente,
    PrestadorPortalDto Prestador,
    EscopoPortal EscopoPortal,
    bool VeContratos,
    Guid? SolicitacaoId);

public sealed record ContatoPortalDto(Guid Id, string Nome, string Email);

public sealed record ClientePortalDto(Guid Id, string Nome);

/// <summary>Identidade do prestador (logotipo no marco de anexos).</summary>
public sealed record PrestadorPortalDto(string Nome);

public sealed record SolicitacaoPortalResumoDto(
    Guid Id, int Numero, string Titulo, StatusExterno StatusExterno, DateTimeOffset AbertaEm, bool PodeAprovar);

public sealed record SolicitacaoPortalDto(
    Guid Id,
    int Numero,
    string Titulo,
    string? Descricao,
    StatusExterno StatusExterno,
    DateTimeOffset AbertaEm,
    ResponsavelPortalDto? Responsavel,
    SlaPortalDto? Sla,
    IReadOnlyList<TarefaPortalDto> TarefasVisiveis,
    int TempoVisivelMinutos,
    bool PodeAprovar);

/// <summary>Só o nome de quem atende.</summary>
public sealed record ResponsavelPortalDto(string Nome);

public sealed record SlaPortalDto(DateTimeOffset PrazoResolucao, SituacaoSla Situacao, bool Pausado);

public sealed record TarefaPortalDto(string Titulo, StatusTarefa Status);

/// <summary>Interação EXTERNA; <see cref="DaEquipe"/> separa as mensagens do prestador das do cliente.</summary>
public sealed record InteracaoPortalDto(
    Guid Id, string? Texto, string AutorNome, bool DaEquipe, DateTimeOffset RegistradaEm, IReadOnlyList<AnexoPortalDto> Anexos);

/// <summary>Anexo visto pelo contato (download em <c>/portal/anexos/{id}/download</c>).</summary>
public sealed record AnexoPortalDto(Guid Id, string NomeArquivo, string TipoConteudo, long TamanhoBytes);

public sealed record ContratoPortalDto(Guid Id, TipoContrato Tipo, DateOnly Inicio, DateOnly? Fim, int? FranquiaMinutos);

/// <summary>Tipo, franquia e saldo do contrato no período, para o cabeçalho do extrato.</summary>
public sealed record SaldoContratoPortal(TipoContrato Tipo, int? FranquiaMinutos, int? SaldoMinutos, int? Percentual);

/// <summary>
/// <c>GET /portal/contratos/{id}/extrato</c> (read side §4): <see cref="ConsumidoMinutos"/> é a soma dos itens; saldo e
/// percentual seguem o read side §3 (nulos sem franquia; acumulados no banco de horas).
/// </summary>
public sealed record ExtratoPortalDto(
    ContratoExtratoPortalDto Contrato,
    MesReferencia Periodo,
    int ConsumidoMinutos,
    int? SaldoMinutos,
    int? Percentual,
    IReadOnlyList<ItemExtratoPortalDto> Itens);

public sealed record ContratoExtratoPortalDto(TipoContrato Tipo, int? FranquiaMinutos);

public sealed record ItemExtratoPortalDto(
    DateOnly Data, int? SolicitacaoNumero, string? SolicitacaoTitulo, string Descricao, int DuracaoMinutos);

/// <summary>Corpo do <c>POST /portal/solicitacoes</c>; a prioridade é sugestão do cliente (padrão MEDIA).</summary>
public sealed record NovaSolicitacaoPortalRequest(string Titulo, string? Descricao, Prioridade? Prioridade);

/// <summary>Corpo do <c>POST /portal/solicitacoes/{id}/interacoes</c> (sempre EXTERNA).</summary>
public sealed record RespostaPortalRequest(string? Texto);

/// <summary>Corpo do <c>POST /portal/solicitacoes/{id}/reprovacao</c>.</summary>
public sealed record ReprovacaoPortalRequest(string? Motivo);
