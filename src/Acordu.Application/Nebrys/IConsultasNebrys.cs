using Acordu.Application.Common;
using Acordu.Domain.Enums;

namespace Acordu.Application.Nebrys;

/// <summary>
/// Leituras da área da Nebrys (ADR 0014, item 3; read side "Área da Nebrys"): só o <c>Tenant</c> e o que é da relação
/// com a Nebrys, nunca dado de negócio do prestador (RNF-PRIV-01). A implementação fica na Infrastructure, num ponto
/// revisado de <c>IgnoreQueryFilters</c>: a sessão de operação não tem tenant, e os filtros não devolveriam nada.
/// </summary>
public interface IConsultasNebrys
{
    /// <summary>Empresas, as criadas por último primeiro, com o filtro opcional de situação (F14-61).</summary>
    Task<ResultadoPaginado<EmpresaResumoNebrysDto>> ListarEmpresasAsync(
        SituacaoEmpresa? situacao, Paginacao paginacao, CancellationToken cancellationToken = default);

    /// <summary>Uma empresa, com a versão para o <c>If-Match</c> das ações (D5); nula se não existe (F14-62).</summary>
    Task<EmpresaNebrysDto?> ObterEmpresaAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Quem criou a empresa (nome e e-mail da conta). Nulo quando a conta não existe mais ou nas empresas sem criador
/// gravado.
/// </summary>
public sealed record CriadorEmpresaDto(string Nome, string? Email);

/// <summary>
/// Linha da lista de empresas (F14-61): só os campos da relação com a Nebrys. Datas da situação como no <c>/auth/eu</c>:
/// <see cref="SomenteLeituraDesde"/> é a data do vencimento na VENCIDA, <see cref="CanceladaEm"/> a do cancelamento, e
/// <see cref="ExpurgoPrevistoEm"/> é calculada (início do só leitura + <c>Empresas__DiasAteExpurgo</c>).
/// </summary>
public sealed record EmpresaResumoNebrysDto(
    Guid Id,
    string Nome,
    string? Documento,
    SituacaoEmpresa Situacao,
    DateTimeOffset CriadaEm,
    DateTimeOffset? FimDoTeste,
    DateTimeOffset? SomenteLeituraDesde,
    DateTimeOffset? CanceladaEm,
    DateTimeOffset? ExpurgoPrevistoEm,
    CriadorEmpresaDto? Criador,
    int PessoasNaEquipe);

/// <summary>
/// Detalhe da empresa na área (F14-62): os mesmos campos da lista e a <see cref="Versao"/> do <c>Tenant</c> (<c>xmin</c>),
/// que as ações do F14l mandam no <c>If-Match</c> (D5). Nada de clientes, contatos, solicitações, tarefas, horas,
/// contratos ou anexos: não há campo nem rota para eles.
/// </summary>
public sealed record EmpresaNebrysDto(
    Guid Id,
    string Nome,
    string? Documento,
    SituacaoEmpresa Situacao,
    DateTimeOffset CriadaEm,
    DateTimeOffset? FimDoTeste,
    DateTimeOffset? SomenteLeituraDesde,
    DateTimeOffset? CanceladaEm,
    DateTimeOffset? ExpurgoPrevistoEm,
    CriadorEmpresaDto? Criador,
    int PessoasNaEquipe,
    string Versao);
