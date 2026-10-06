using Acordu.Domain.Enums;

namespace Acordu.Application.Clientes;

/// <summary>Item da listagem de clientes.</summary>
public sealed record ClienteResumoDto(Guid Id, string Nome, string? Documento, bool Ativo);

/// <summary>Detalhe do cliente, com contatos (contrato §4.1).</summary>
public sealed record ClienteDto(
    Guid Id, string Nome, string? Documento, string? Observacoes, bool Ativo, string Versao, IReadOnlyList<ContatoDto> Contatos);

/// <summary>
/// Contato do cliente; <see cref="TemConta"/> indica login no portal. <see cref="AcessoPortal"/> é nulo sem acesso nem
/// convite pendente.
/// </summary>
public sealed record ContatoDto(
    Guid Id, string Nome, string Email, string? Telefone, string? Cargo, bool Ativo, bool TemConta, AcessoPortalContatoDto? AcessoPortal);

/// <summary>
/// Acesso do contato ao portal: <see cref="SituacaoAcessoPortal.Convidado"/> (convite pendente, com a expiração do link
/// enviado; nula enquanto o e-mail não saiu) ou <see cref="SituacaoAcessoPortal.Ativo"/>.
/// </summary>
public sealed record AcessoPortalContatoDto(SituacaoAcessoPortal Situacao, EscopoPortal EscopoPortal, DateTimeOffset? ConviteExpiraEm);

public enum SituacaoAcessoPortal
{
    Convidado = 1,
    Ativo = 2,
}

/// <summary>
/// Corpo do <c>POST</c> e do <c>PATCH /clientes</c>. O PATCH recebe todos os campos editáveis (o formulário inteiro):
/// ausente ou nulo apaga o campo opcional.
/// </summary>
public sealed record ClienteRequest(string Nome, string? Documento, string? Observacoes);

/// <summary>Corpo do <c>POST</c> e do <c>PATCH</c> de contato, com a mesma regra do <see cref="ClienteRequest"/>.</summary>
public sealed record ContatoRequest(string Nome, string Email, string? Telefone, string? Cargo);
