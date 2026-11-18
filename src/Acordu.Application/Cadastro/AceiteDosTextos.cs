namespace Acordu.Application.Cadastro;

/// <summary>
/// Conferência do aceite dos textos legais no cadastro e no aceite de convite da equipe (RF-AUT-11; design do F14, D10 e
/// D16). Erro de campo (<c>400</c>) como as demais recusas do cadastro, sem consultar o banco.
/// </summary>
public static class AceiteDosTextos
{
    public const string CampoTermos = "versaoTermos";
    public const string CampoPrivacidade = "versaoPrivacidade";

    public const string AceiteObrigatorio = "Para continuar, aceite os termos de uso e a política de privacidade.";

    public const string VersaoDesatualizada =
        "Os termos de uso ou a política de privacidade mudaram. Recarregue a página, leia a versão nova e aceite de novo.";

    /// <summary>
    /// Verdadeiro quando o aceite deve ser gravado (textos publicados e as versões vigentes recebidas). Textos não
    /// publicados: falso, sem conferir nada, e o que vier é ignorado (F14-115, F14-116, F14-117). Publicados, a falta de
    /// uma versão ou uma versão diferente da vigente lança <see cref="ArgumentException"/> no campo dela (F14-80).
    /// </summary>
    public static bool Conferir(OpcoesTextosLegais opcoes, string? versaoTermos, string? versaoPrivacidade)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        if (!opcoes.Publicados)
            return false;
        Conferir(versaoTermos, TextosLegais.VersaoTermos, CampoTermos);
        Conferir(versaoPrivacidade, TextosLegais.VersaoPrivacidade, CampoPrivacidade);
        return true;
    }

    private static void Conferir(string? recebida, string vigente, string campo)
    {
        var versao = recebida?.Trim();
        if (string.IsNullOrEmpty(versao))
            throw new ArgumentException(AceiteObrigatorio, campo);
        if (!string.Equals(versao, vigente, StringComparison.Ordinal))
            throw new ArgumentException(VersaoDesatualizada, campo);
    }
}
