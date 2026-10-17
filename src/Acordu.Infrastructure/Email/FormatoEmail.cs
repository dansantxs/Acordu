using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Acordu.Infrastructure.Email;

/// <summary>Formato comum aos envios (Resend e SMTP): remetente e HTML gerado do texto.</summary>
internal static partial class FormatoEmail
{
    /// <summary>Nome do remetente sem caracteres que quebrariam o cabeçalho; vazio vira o nome padrão.</summary>
    public static string NomeRemetente(string? nome, OpcoesEmail opcoes)
    {
        var limpo = Regex.Replace(nome ?? string.Empty, "[\"<>\\r\\n,;]", string.Empty).Trim();
        return limpo.Length == 0 ? opcoes.NomePadrao : limpo;
    }

    /// <summary>Texto escapado, parágrafos preservados e URLs http(s) como links.</summary>
    public static string Html(string texto)
    {
        var escapado = WebUtility.HtmlEncode(texto.Replace("\r\n", "\n", StringComparison.Ordinal));
        var comLinks = Url().Replace(escapado, m => $"<a href=\"{m.Value}\">{m.Value}</a>");
        var html = new StringBuilder("<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:15px;line-height:1.5;color:#1f2933\">");
        html.Append(comLinks.Replace("\n", "<br>", StringComparison.Ordinal));
        html.Append("</div>");
        return html.ToString();
    }

    [GeneratedRegex(@"https?://[^\s<>""]+")]
    private static partial Regex Url();
}
