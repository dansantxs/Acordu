using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace Acordu.Api.FunctionalTests.Apoio;

/// <summary>Webhook do Discord de mentira: guarda cada mensagem (URL e JSON) e responde como mandarem.</summary>
internal sealed class DiscordFalso : HttpMessageHandler
{
    public const string Alertas = "https://discord.com/api/webhooks/1/alertas-dos-testes";
    public const string Deploy = "https://discord.com/api/webhooks/2/deploy-dos-testes";
    public const string Sugestoes = "https://discord.com/api/webhooks/3/sugestoes-dos-testes";

    private readonly Func<CancellationToken, Task<HttpResponseMessage>> _responder;

    public DiscordFalso(Func<CancellationToken, Task<HttpResponseMessage>>? responder = null) =>
        _responder = responder ?? (_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));

    public ConcurrentQueue<(Uri Webhook, JsonElement Corpo)> Mensagens { get; } = new();

    /// <summary>Os textos enviados a um webhook, em ordem.</summary>
    public IReadOnlyList<string> Textos(string webhook) =>
        Mensagens.Where(m => m.Webhook == new Uri(webhook)).Select(m => m.Corpo.GetProperty("content").GetString()!).ToList();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var corpo = await request.Content!.ReadAsStringAsync(cancellationToken);
        Mensagens.Enqueue((request.RequestUri!, JsonDocument.Parse(corpo).RootElement.Clone()));
        return await _responder(cancellationToken);
    }
}
