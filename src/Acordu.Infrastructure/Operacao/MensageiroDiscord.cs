using System.Net.Http.Json;
using Acordu.Application.Abstractions.Operacao;
using Microsoft.Extensions.Logging;

namespace Acordu.Infrastructure.Operacao;

/// <summary>Webhooks dos canais da operação (<c>Discord__Alertas</c>, <c>__Deploy</c>, <c>__Sugestoes</c>); sem, o canal não envia.</summary>
public sealed record OpcoesDiscord(Uri? Alertas, Uri? Deploy, Uri? Sugestoes)
{
    public static readonly OpcoesDiscord Desligado = new(null, null, null);

    /// <summary>Sem as URLs: o <c>ToString</c> gerado do record imprimiria os segredos.</summary>
    public override string ToString() =>
        $"OpcoesDiscord {{ Alertas = {Alertas is not null}, Deploy = {Deploy is not null}, Sugestoes = {Sugestoes is not null} }}";

    /// <summary>Webhook informado: só https (é um segredo, e o Discord só aceita assim).</summary>
    public static Uri? Webhook(string? valor, string nome)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;
        if (!Uri.TryCreate(valor.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException($"{nome} precisa ser uma URL https de webhook.");
        return uri;
    }

    internal Uri? De(CanalOperacao canal) => canal switch
    {
        CanalOperacao.Alertas => Alertas,
        CanalOperacao.Deploy => Deploy,
        CanalOperacao.Sugestoes => Sugestoes,
        _ => null,
    };
}

/// <summary>
/// Mensagem ao webhook do Discord por HTTP, sem SDK (ADR 0012): <c>content</c> até 2.000 caracteres (limite do Discord)
/// e <c>allowed_mentions</c> vazio, para nenhum texto acionar <c>@everyone</c>, <c>@here</c> ou um cargo (F13-11). Falha
/// vai para o log como aviso, sem a URL (é segredo).
/// </summary>
public sealed partial class MensageiroDiscord : IMensageiroOperacao
{
    public const int LimiteTexto = 2000;

    private readonly HttpClient _http;
    private readonly OpcoesDiscord _opcoes;
    private readonly ILogger<MensageiroDiscord> _logger;

    public MensageiroDiscord(HttpClient http, OpcoesDiscord opcoes, ILogger<MensageiroDiscord> logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(logger);
        _http = http;
        _opcoes = opcoes;
        _logger = logger;
    }

    public bool Configurado(CanalOperacao canal) => _opcoes.De(canal) is not null;

    public async Task<bool> EnviarAsync(CanalOperacao canal, string texto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texto);
        if (_opcoes.De(canal) is not { } webhook)
            return false;

        var conteudo = texto.Length <= LimiteTexto ? texto : string.Concat(texto.AsSpan(0, LimiteTexto - 1), "…");
        try
        {
            using var resposta = await _http.PostAsJsonAsync(
                webhook, new { content = conteudo, allowed_mentions = new { parse = Array.Empty<string>() } }, cancellationToken);
            if (resposta.IsSuccessStatusCode)
                return true;
            NaoSaiu(_logger, canal, (int)resposta.StatusCode);
            return false;
        }
        // Rede, timeout do HttpClient ou o prazo de quem chama: nada disso pode subir (a resposta ao usuário não muda).
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            Falhou(_logger, canal, ex.GetType().Name);
            return false;
        }
    }

    [LoggerMessage(EventId = 600, Level = LogLevel.Warning, Message = "Aviso de operação ({Canal}) não saiu: HTTP {Status}.")]
    private static partial void NaoSaiu(ILogger logger, CanalOperacao canal, int status);

    // Só o tipo da exceção: a mensagem do HttpRequestException pode trazer a URL do webhook.
    [LoggerMessage(EventId = 601, Level = LogLevel.Warning, Message = "Aviso de operação ({Canal}) não saiu: {Falha}.")]
    private static partial void Falhou(ILogger logger, CanalOperacao canal, string falha);
}
