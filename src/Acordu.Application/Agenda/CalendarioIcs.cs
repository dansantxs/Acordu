using System.Globalization;
using System.Text;

namespace Acordu.Application.Agenda;

/// <summary>
/// Escrita mínima de iCalendar (RFC 5545): linhas em CRLF dobradas em 75 octetos, texto escapado, datas de dia inteiro
/// (<c>VALUE=DATE</c>) e instantes em UTC.
/// </summary>
internal sealed class CalendarioIcs
{
    private readonly StringBuilder _texto = new();

    public CalendarioIcs(string nome)
    {
        Linha("BEGIN:VCALENDAR");
        Linha("VERSION:2.0");
        Linha("PRODID:-//Nebrys//Acordu//PT-BR");
        Linha("CALSCALE:GREGORIAN");
        Linha("METHOD:PUBLISH");
        Linha("X-WR-CALNAME:" + Escapar(nome));
        Linha("X-PUBLISHED-TTL:PT1H");
        Linha("REFRESH-INTERVAL;VALUE=DURATION:PT1H");
    }

    public void DiaInteiro(string uid, DateOnly dia, string titulo, string? descricao, Uri link, DateTimeOffset agora)
    {
        Inicio(uid, titulo, descricao, link, agora);
        Linha("DTSTART;VALUE=DATE:" + dia.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        Linha("DTEND;VALUE=DATE:" + dia.AddDays(1).ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        Linha("TRANSP:TRANSPARENT");
        Linha("END:VEVENT");
    }

    public void Instante(string uid, DateTimeOffset inicio, TimeSpan duracao, string titulo, string? descricao, Uri link, DateTimeOffset agora)
    {
        Inicio(uid, titulo, descricao, link, agora);
        Linha("DTSTART:" + Utc(inicio));
        Linha("DTEND:" + Utc(inicio + duracao));
        Linha("END:VEVENT");
    }

    public string Fechar()
    {
        Linha("END:VCALENDAR");
        return _texto.ToString();
    }

    internal static string Escapar(string texto) =>
        texto.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal);

    private static string Utc(DateTimeOffset instante) =>
        instante.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private void Inicio(string uid, string titulo, string? descricao, Uri link, DateTimeOffset agora)
    {
        Linha("BEGIN:VEVENT");
        Linha("UID:" + uid);
        Linha("DTSTAMP:" + Utc(agora));
        Linha("SUMMARY:" + Escapar(titulo));
        if (!string.IsNullOrEmpty(descricao))
            Linha("DESCRIPTION:" + Escapar(descricao));
        Linha("URL:" + link.AbsoluteUri);
    }

    /// <summary>Dobra em 75 octetos UTF-8 sem partir caractere; a continuação começa com um espaço.</summary>
    private void Linha(string conteudo)
    {
        const int Limite = 75;
        var bytes = 0;
        foreach (var rune in conteudo.EnumerateRunes())
        {
            var tamanho = rune.Utf8SequenceLength;
            if (bytes + tamanho > Limite)
            {
                _texto.Append("\r\n ");
                bytes = 1;
            }

            _texto.Append(rune.ToString());
            bytes += tamanho;
        }

        _texto.Append("\r\n");
    }
}
