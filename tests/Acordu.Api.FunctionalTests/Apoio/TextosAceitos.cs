using Acordu.Application.Cadastro;

namespace Acordu.Api.FunctionalTests.Apoio;

/// <summary>
/// As versões vigentes dos textos legais, que o cadastro e o aceite de convite da equipe mandam: a fixture liga
/// <c>TextosLegais__Publicados</c> (design do F14, §1.2), então todo cadastro dos testes aceita os textos (F14h).
/// </summary>
internal static class TextosAceitos
{
    public const string Termos = TextosLegais.VersaoTermos;

    public const string Privacidade = TextosLegais.VersaoPrivacidade;
}
