using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Acordu.Application.Abstractions;

namespace Acordu.Infrastructure.Email;

/// <summary>
/// Envio pela API HTTP do Resend (<c>POST /emails</c>), sem SDK (ADR 0006). Texto e HTML do mesmo conteúdo: o HTML é o
/// texto escapado, com quebras de linha e links clicáveis. Resposta fora de 2xx lança exceção (a outbox tenta de novo),
/// sem o corpo da mensagem no erro.
/// </summary>
public sealed class EnviadorEmailResend : IEnviadorEmail
{
    public static readonly Uri Endpoint = new("https://api.resend.com/emails");

    private readonly HttpClient _http;
    private readonly OpcoesEmail _opcoes;

    public EnviadorEmailResend(HttpClient http, OpcoesEmail opcoes)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(opcoes);
        if (!opcoes.UsaResend)
            throw new ArgumentException("Chave do Resend não configurada.", nameof(opcoes));
        _http = http;
        _opcoes = opcoes;
    }

    public async Task EnviarAsync(MensagemEmail mensagem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(new CorpoResend(
                De(mensagem.NomeRemetente),
                [mensagem.Para],
                mensagem.Assunto,
                mensagem.Texto,
                FormatoEmail.Html(mensagem.Texto))),
        };
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opcoes.ChaveResend);

        using var resposta = await _http.SendAsync(requisicao, cancellationToken);
        if (!resposta.IsSuccessStatusCode)
        {
            // Sem o destinatário nem o conteúdo: só a categoria e o status (segurança §6).
            throw new HttpRequestException(
                $"Resend recusou o e-mail '{mensagem.Categoria}': {(int)resposta.StatusCode} {resposta.ReasonPhrase}.",
                null,
                resposta.StatusCode);
        }
    }

    /// <summary>"Nome &lt;endereço&gt;".</summary>
    private string De(string? nome) => $"{FormatoEmail.NomeRemetente(nome, _opcoes)} <{_opcoes.Remetente}>";

    private sealed record CorpoResend(
        [property: JsonPropertyName("from")] string De,
        [property: JsonPropertyName("to")] IReadOnlyList<string> Para,
        [property: JsonPropertyName("subject")] string Assunto,
        [property: JsonPropertyName("text")] string Texto,
        [property: JsonPropertyName("html")] string Html);
}
