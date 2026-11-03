using System.Globalization;
using Acordu.Domain.Entities;

namespace Acordu.Api.Composicao;

/// <summary>
/// Confere, ao subir, o que o SLA e a formatação precisam do sistema operacional: o banco de fusos IANA (tzdata) e o
/// ICU (globalização completa). Numa imagem sem eles a API calcularia prazos errados ou cairia na primeira requisição;
/// assim ela nem sobe, e o healthcheck do deploy volta para a revisão anterior (deploy-ci §2.3, spec do F8, F8-16).
/// </summary>
internal static class AmbienteDeExecucao
{
    public static void Conferir()
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(CalendarioUtil.FusoPadrao);
        }
        catch (TimeZoneNotFoundException erro)
        {
            throw new InvalidOperationException(
                $"Fuso '{CalendarioUtil.FusoPadrao}' não encontrado: a imagem precisa do tzdata.", erro);
        }

        try
        {
            _ = CultureInfo.GetCultureInfo("pt-BR", predefinedOnly: true);
        }
        catch (CultureNotFoundException erro)
        {
            throw new InvalidOperationException(
                "Cultura pt-BR indisponível: a imagem precisa do ICU (globalização invariante não serve).", erro);
        }
    }
}
