using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Api.FunctionalTests.Apoio;

/// <summary>
/// O e-mail que o Worker de entrada repassa à rota <c>/internal/email/entrada</c> (design do F15, D1 e D11): o MIME bruto
/// e os cabeçalhos do envelope e dos vereditos, como o <c>acordu-email-entrada</c> manda.
/// </summary>
internal static class EntradaPorEmail
{
    public const string Rota = "/internal/email/entrada";

    /// <summary>O veredito do Worker quando o domínio do remetente confirma que o e-mail saiu dele (D11).</summary>
    public const string Autenticado = "pass";

    public static string NovoMessageId() => $"<{Guid.NewGuid():N}@mail.bomgrao.com.br>";

    /// <summary>Um e-mail simples, em texto ou no <paramref name="tipo"/> pedido, com os <paramref name="cabecalhos"/> a mais.</summary>
    public static byte[] Mime(
        string de,
        string? assunto,
        string? messageId,
        string corpo = "O forno parou de novo hoje cedo.",
        string tipo = "text/plain; charset=utf-8",
        string cabecalhos = "",
        string para = "atendimento@estudiotraco.com.br") =>
        Encoding.UTF8.GetBytes(
            $"From: {de}\r\n"
            + $"To: {para}\r\n"
            + (assunto is null ? string.Empty : $"Subject: {assunto}\r\n")
            + (messageId is null ? string.Empty : $"Message-ID: {messageId}\r\n")
            + "Date: Tue, 06 Oct 2026 10:00:00 -0300\r\n"
            + cabecalhos
            + "MIME-Version: 1.0\r\n"
            + $"Content-Type: {tipo}\r\n"
            + "\r\n"
            + corpo + "\r\n");

    /// <summary>O endereço de entrada da empresa (<c>{código}@{domínio}</c>).</summary>
    public static async Task<string> EnderecoAsync(ApiFixture api, Guid tenantId)
    {
        await using var db = api.DaEquipe(tenantId);
        var codigo = (await db.Tenants.AsNoTracking().SingleAsync()).CodigoEntrada!;
        return $"{codigo}@{ApiFixture.DominioEntrada}";
    }

    public static async Task LigarAsync(Equipe equipe, bool ligado = true) =>
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, "/api/v1/configuracoes/recebimento-email", new { ligado });

    /// <summary>Entrega como o Worker: token, envelope, autenticidade e filtro <c>ok</c>; espera o <c>202</c>.</summary>
    public static async Task EntregarAsync(
        ApiFixture api, string destino, byte[] mime, string envelope, string? autenticidade = Autenticado)
    {
        using var http = api.NovoHttp();
        using var conteudo = new ByteArrayContent(mime);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("message/rfc822");
        using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri(Rota, UriKind.Relative)) { Content = conteudo };
        pedido.Headers.TryAddWithoutValidation("X-Email-Token", ApiFixture.TokenEntrada);
        pedido.Headers.TryAddWithoutValidation("X-Email-Destinatario", destino);
        pedido.Headers.TryAddWithoutValidation("X-Email-Remetente", envelope);
        pedido.Headers.TryAddWithoutValidation("X-Email-Filtro", "ok");
        if (autenticidade is not null)
            pedido.Headers.TryAddWithoutValidation("X-Email-Autenticidade", autenticidade);
        using var resposta = await http.SendAsync(pedido);
        var texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.Accepted, "corpo: {0}", texto);
        texto.Should().BeEmpty("a rota não diz o que fez com o e-mail");
    }
}
