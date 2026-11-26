using System.Text.RegularExpressions;
using Acordu.Domain.Entities;

namespace Acordu.Application.EntradaEmail;

/// <summary>
/// Configuração da entrada por e-mail (design do F15, D2 e §1.2): <c>EmailEntrada__Dominio</c>, o domínio dos endereços de
/// entrada (em produção <c>entrada.acordu.app.br</c>, variável <c>EMAIL_ENTRADA_DOMINIO</c> do <c>deploy.yml</c>). Sem ele
/// (desenvolvimento sem entrada de e-mail), nenhuma empresa tem endereço e ligar o recebimento é recusado (F15-94). Valor
/// presente e inválido impede a subida.
/// </summary>
public sealed record OpcoesEmailEntrada
{
    public const string ChaveDominio = "EmailEntrada__Dominio";

    /// <param name="dominio">Já normalizado (minúsculas, sem espaços); nulo = sem entrada de e-mail.</param>
    public OpcoesEmailEntrada(string? dominio = null)
    {
        if (dominio is not null && !FormatoDoDominio.Valido().IsMatch(dominio))
            throw new ArgumentException("Domínio de entrada inválido.", nameof(dominio));
        Dominio = dominio;
    }

    /// <summary>O domínio dos endereços de entrada, ou nulo sem entrada de e-mail.</summary>
    public string? Dominio { get; }

    /// <summary>Há domínio de entrada: as empresas com código têm endereço e podem ligar o recebimento.</summary>
    public bool Disponivel => Dominio is not null;

    /// <summary>O endereço de entrada da empresa (<c>{código}@{domínio}</c>), ou nulo sem domínio ou sem código (demonstração).</summary>
    public string? EnderecoDe(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return Dominio is null || tenant.CodigoEntrada is null ? null : tenant.CodigoEntrada + "@" + Dominio;
    }

    /// <summary>
    /// Os avisos da empresa respondem para o endereço da conversa (F15-51, F15-52): há domínio, a empresa tem código e o
    /// recebimento por e-mail está ligado.
    /// </summary>
    public bool AvisosPelaConversa(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return Dominio is not null && tenant.CodigoEntrada is not null && tenant.RecebimentoEmail;
    }

    /// <summary>
    /// O endereço da conversa (<c>{código}+{token}@{domínio}</c>, design do F15, D3), ou nulo sem domínio ou sem código.
    /// </summary>
    public string? EnderecoDaConversa(Tenant tenant, ConversaPorEmail conversa)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(conversa);
        if (conversa.TenantId != tenant.Id)
            throw new ArgumentException("A conversa é de outra empresa.", nameof(conversa));
        return Dominio is null || tenant.CodigoEntrada is null ? null : tenant.CodigoEntrada + "+" + conversa.Token + "@" + Dominio;
    }

    /// <summary>
    /// Ausente ou vazio → sem entrada de e-mail; um nome de domínio (rótulos de letras, dígitos e hífen, com ao menos um
    /// ponto), aparado e sem diferenciar maiúsculas → o próprio, em minúsculas; outro valor impede a subida.
    /// </summary>
    public static OpcoesEmailEntrada DaConfiguracao(string? dominio)
    {
        var texto = dominio?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(texto))
            return new OpcoesEmailEntrada();
        if (texto.Length > 253 || !FormatoDoDominio.Valido().IsMatch(texto))
            throw new InvalidOperationException($"{ChaveDominio} precisa ser um nome de domínio (ex.: entrada.acordu.app.br).");
        return new OpcoesEmailEntrada(texto);
    }
}

/// <summary>Nome de domínio em minúsculas: rótulos de letras, dígitos e hífen (sem hífen nas pontas), com ao menos um ponto.</summary>
internal static partial class FormatoDoDominio
{
    [GeneratedRegex(@"^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$", RegexOptions.CultureInvariant)]
    public static partial Regex Valido();
}
