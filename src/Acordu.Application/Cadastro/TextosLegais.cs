using Acordu.Domain.Enums;

namespace Acordu.Application.Cadastro;

/// <summary>
/// Versões vigentes dos textos legais (design do F14, D10; convenções §4). **Mudar o texto é mudar a versão**: no mesmo
/// PR, o cabeçalho do arquivo em <c>apps/web/src/features/legal/textos/</c>, a constante <c>VERSOES_TEXTOS_LEGAIS</c> do
/// SPA e estas constantes. Elas vão ao OpenAPI como o <c>enum</c> de <c>versaoTermos</c> e <c>versaoPrivacidade</c>, e o
/// teste do SPA (<c>versoes.test.ts</c>) confere o arquivo contra o OpenAPI.
/// </summary>
public static class TextosLegais
{
    public const string VersaoTermos = "1";

    public const string VersaoPrivacidade = "1";

    /// <summary>Os textos, na ordem em que aparecem nas respostas.</summary>
    public static readonly IReadOnlyList<TipoTextoLegal> Todos = [TipoTextoLegal.Termos, TipoTextoLegal.Privacidade];

    /// <summary>A versão vigente do texto.</summary>
    public static string Vigente(TipoTextoLegal texto) => texto switch
    {
        TipoTextoLegal.Termos => VersaoTermos,
        TipoTextoLegal.Privacidade => VersaoPrivacidade,
        _ => throw new ArgumentOutOfRangeException(nameof(texto), texto, "Texto legal desconhecido."),
    };
}
