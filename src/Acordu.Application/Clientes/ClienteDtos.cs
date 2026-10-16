using Acordu.Application.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Clientes;

/// <summary>Item da listagem de clientes.</summary>
public sealed record ClienteResumoDto(
    Guid Id, string Nome, string? Documento, bool Ativo, string? RazaoSocial, Referencia? Responsavel, IReadOnlyList<string> Etiquetas);

/// <summary>Detalhe do cliente, com contatos (contrato §4.1; cadastro ampliado em 16/10).</summary>
public sealed record ClienteDto(
    Guid Id,
    string Nome,
    string? Documento,
    string? Observacoes,
    bool Ativo,
    string Versao,
    IReadOnlyList<ContatoDto> Contatos,
    string? RazaoSocial,
    string? InscricaoEstadual,
    string? Email,
    string? Telefone,
    EnderecoDto? Endereco,
    Referencia? Responsavel,
    IReadOnlyList<string> Etiquetas);

/// <summary>Endereço no corpo e na resposta; CEP, logradouro, cidade e UF obrigatórios quando há endereço.</summary>
public sealed record EnderecoDto(string Cep, string Logradouro, string? Numero, string? Complemento, string? Bairro, string Cidade, string Uf)
{
    public static EnderecoDto? De(Endereco? endereco) => endereco is null
        ? null
        : new(endereco.Cep, endereco.Logradouro, endereco.Numero, endereco.Complemento, endereco.Bairro, endereco.Cidade, endereco.Uf);
}

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
public sealed record ClienteRequest(
    string Nome,
    string? Documento,
    string? Observacoes,
    string? RazaoSocial = null,
    string? InscricaoEstadual = null,
    string? Email = null,
    string? Telefone = null,
    EnderecoDto? Endereco = null,
    Guid? ResponsavelId = null,
    IReadOnlyList<string>? Etiquetas = null);

/// <summary>Corpo do <c>POST</c> e do <c>PATCH</c> de contato, com a mesma regra do <see cref="ClienteRequest"/>.</summary>
public sealed record ContatoRequest(string Nome, string Email, string? Telefone, string? Cargo);
