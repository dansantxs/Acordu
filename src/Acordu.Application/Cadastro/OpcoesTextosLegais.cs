namespace Acordu.Application.Cadastro;

/// <summary>
/// Se os textos legais estão publicados (design do F14, D16): <c>TextosLegais__Publicados</c>, que em produção vem da
/// variável <c>TEXTOS_LEGAIS_PUBLICADOS</c> do GitHub, a mesma que liga as páginas, os links e a caixa de aceite no SPA
/// (<c>VITE_TEXTOS_LEGAIS_PUBLICADOS</c>). **Padrão desligado.** Desligada: o cadastro e o aceite de convite da equipe
/// não exigem nem gravam aceite (F14-115 a F14-117), o <c>/auth/eu</c> não lista pendentes e o "ciente" responde
/// <c>404</c> (F14-118). Ligada: o aceite é obrigatório (F14-80, F14-82) e quem não tem a versão vigente recebe o aviso
/// (F14-83, F14-119).
/// </summary>
public sealed record OpcoesTextosLegais
{
    public const string Chave = "TextosLegais__Publicados";

    public OpcoesTextosLegais(bool publicados = false)
    {
        Publicados = publicados;
    }

    public bool Publicados { get; }

    /// <summary>
    /// Ausente ou vazia: desligada. <c>true</c> ou <c>false</c>, aparado e sem diferenciar maiúsculas: o próprio. Qualquer
    /// outro valor impede a subida (o <c>deploy.yml</c> já barra antes de publicar, F14-121).
    /// </summary>
    public static OpcoesTextosLegais DaConfiguracao(string? valor)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto))
            return new OpcoesTextosLegais();
        if (string.Equals(texto, "true", StringComparison.OrdinalIgnoreCase))
            return new OpcoesTextosLegais(true);
        if (string.Equals(texto, "false", StringComparison.OrdinalIgnoreCase))
            return new OpcoesTextosLegais(false);
        throw new InvalidOperationException($"{Chave} precisa ser true ou false.");
    }
}
