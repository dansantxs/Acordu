using System.Globalization;

namespace Acordu.Application.Cadastro;

/// <summary>
/// Como o cadastro de empresa abre (RF-AUT-08; decisão 6 do G2 do F14; design D11): só por convite da Nebrys ou aberto a
/// qualquer pessoa (com as demais travas: Turnstile, limite por IP, documento, e-mail e aceite).
/// </summary>
public enum ModoCadastro
{
    /// <summary>O registro só passa com um convite de empresa válido para o e-mail (F14-45, F14-47).</summary>
    Convite = 1,

    /// <summary>O registro passa sem convite, como antes do F14 (F14-50, F14p).</summary>
    Aberto = 2,
}

/// <summary>
/// Configuração do cadastro (design do F14, D11 e D12): <c>Cadastro__Modo</c> (<c>convite</c> ou <c>aberto</c>) e
/// <c>Cadastro__DiasConvite</c> (validade do convite de empresa, padrão 7 dias, decisão 7 do G2). **Sem o modo, vale
/// <c>convite</c>** (fail-closed: um esquecimento nunca abre o cadastro); em produção o <c>deploy.yml</c> exige a
/// variável <c>CADASTRO_MODO</c>. Valor presente e inválido impede a subida.
/// </summary>
public sealed record OpcoesCadastro
{
    public const string ChaveModo = "Cadastro__Modo";
    public const string ChaveDiasConvite = "Cadastro__DiasConvite";
    public const int DiasConvitePadrao = 7;

    public OpcoesCadastro(ModoCadastro modo = ModoCadastro.Convite, int diasConvite = DiasConvitePadrao)
    {
        if (!Enum.IsDefined(modo))
            throw new ArgumentOutOfRangeException(nameof(modo), modo, "Modo do cadastro desconhecido.");
        ArgumentOutOfRangeException.ThrowIfLessThan(diasConvite, 1);
        Modo = modo;
        DiasConvite = diasConvite;
    }

    public ModoCadastro Modo { get; }

    /// <summary>Dias de validade de cada link de convite de empresa, contados do envio (emissão ou reenvio).</summary>
    public int DiasConvite { get; }

    public TimeSpan ValidadeConvite => TimeSpan.FromDays(DiasConvite);

    /// <summary>
    /// Modo: ausente ou vazio → <c>convite</c>; <c>convite</c> ou <c>aberto</c>, aparado e sem diferenciar maiúsculas → o
    /// próprio; outro valor impede a subida. Dias do convite: ausente → 7; inteiro maior que zero → o próprio; outro valor
    /// impede a subida.
    /// </summary>
    public static OpcoesCadastro DaConfiguracao(string? modo, string? diasConvite)
    {
        var textoModo = modo?.Trim();
        ModoCadastro modoLido;
        if (string.IsNullOrEmpty(textoModo) || string.Equals(textoModo, "convite", StringComparison.OrdinalIgnoreCase))
            modoLido = ModoCadastro.Convite;
        else if (string.Equals(textoModo, "aberto", StringComparison.OrdinalIgnoreCase))
            modoLido = ModoCadastro.Aberto;
        else
            throw new InvalidOperationException($"{ChaveModo} precisa ser convite ou aberto.");

        var textoDias = diasConvite?.Trim();
        var dias = DiasConvitePadrao;
        if (!string.IsNullOrEmpty(textoDias)
            && (!int.TryParse(textoDias, NumberStyles.Integer, CultureInfo.InvariantCulture, out dias) || dias < 1))
        {
            throw new InvalidOperationException($"{ChaveDiasConvite} precisa ser um número inteiro de dias maior que zero.");
        }

        return new OpcoesCadastro(modoLido, dias);
    }
}
