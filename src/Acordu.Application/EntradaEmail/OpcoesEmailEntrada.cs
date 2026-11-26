using System.Globalization;
using System.Text.RegularExpressions;
using Acordu.Domain.Entities;

namespace Acordu.Application.EntradaEmail;

/// <summary>
/// Configuração da entrada por e-mail (design do F15, D2 e §1.2): <c>EmailEntrada__Dominio</c>, o domínio dos endereços de
/// entrada (em produção <c>entrada.acordu.app.br</c>, variável <c>EMAIL_ENTRADA_DOMINIO</c> do <c>deploy.yml</c>). Sem ele
/// (desenvolvimento sem entrada de e-mail), nenhuma empresa tem endereço e ligar o recebimento é recusado (F15-94); e
/// <c>EmailEntrada__TamanhoMaximoBytes</c>, o tamanho máximo do e-mail recebido (padrão 25 MiB, o teto do provedor; D12,
/// F15-89). Valor presente e inválido impede a subida.
/// </summary>
public sealed record OpcoesEmailEntrada
{
    public const string ChaveDominio = "EmailEntrada__Dominio";

    public const string ChaveTamanhoMaximo = "EmailEntrada__TamanhoMaximoBytes";

    /// <summary>25 MiB: o maior e-mail que o Cloudflare Email Routing entrega (design D1 e D12; pergunta 10 do G2).</summary>
    public const long TamanhoMaximoPadrao = 25L * 1024 * 1024;

    /// <param name="dominio">Já normalizado (minúsculas, sem espaços); nulo = sem entrada de e-mail.</param>
    /// <param name="tamanhoMaximoBytes">De 1 byte a <see cref="TamanhoMaximoPadrao"/> (acima dele o provedor não entrega).</param>
    public OpcoesEmailEntrada(string? dominio = null, long tamanhoMaximoBytes = TamanhoMaximoPadrao)
    {
        if (dominio is not null && !FormatoDoDominio.Valido().IsMatch(dominio))
            throw new ArgumentException("Domínio de entrada inválido.", nameof(dominio));
        ArgumentOutOfRangeException.ThrowIfLessThan(tamanhoMaximoBytes, 1L);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tamanhoMaximoBytes, TamanhoMaximoPadrao);
        Dominio = dominio;
        TamanhoMaximoBytes = tamanhoMaximoBytes;
    }

    /// <summary>O domínio dos endereços de entrada, ou nulo sem entrada de e-mail.</summary>
    public string? Dominio { get; }

    /// <summary>
    /// Tamanho máximo do e-mail recebido, em bytes (o MIME inteiro): o Worker recusa a entrega acima dele, e a rota de
    /// entrada responde <c>413</c> sem gravar (F15-89).
    /// </summary>
    public long TamanhoMaximoBytes { get; }

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
    /// Domínio ausente ou vazio → sem entrada de e-mail; um nome de domínio (rótulos de letras, dígitos e hífen, com ao
    /// menos um ponto), aparado e sem diferenciar maiúsculas → o próprio, em minúsculas. Tamanho ausente → o padrão; um
    /// inteiro de 1 a <see cref="TamanhoMaximoPadrao"/> → o próprio. Outro valor, em qualquer dos dois, impede a subida.
    /// </summary>
    public static OpcoesEmailEntrada DaConfiguracao(string? dominio, string? tamanhoMaximoBytes = null) =>
        new(LerDominio(dominio), LerTamanhoMaximo(tamanhoMaximoBytes));

    private static string? LerDominio(string? dominio)
    {
        var texto = dominio?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(texto))
            return null;
        if (texto.Length > 253 || !FormatoDoDominio.Valido().IsMatch(texto))
            throw new InvalidOperationException($"{ChaveDominio} precisa ser um nome de domínio (ex.: entrada.acordu.app.br).");
        return texto;
    }

    private static long LerTamanhoMaximo(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return TamanhoMaximoPadrao;
        return long.TryParse(texto.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var bytes)
            && bytes is >= 1 and <= TamanhoMaximoPadrao
            ? bytes
            : throw new InvalidOperationException(
                $"{ChaveTamanhoMaximo} precisa ser um número inteiro de bytes de 1 a {TamanhoMaximoPadrao.ToString(CultureInfo.InvariantCulture)}.");
    }
}

/// <summary>Nome de domínio em minúsculas: rótulos de letras, dígitos e hífen (sem hífen nas pontas), com ao menos um ponto.</summary>
internal static partial class FormatoDoDominio
{
    [GeneratedRegex(@"^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$", RegexOptions.CultureInvariant)]
    public static partial Regex Valido();
}
