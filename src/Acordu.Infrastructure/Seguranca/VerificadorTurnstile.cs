using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Application.Autenticacao;
using Microsoft.Extensions.Logging;

namespace Acordu.Infrastructure.Seguranca;

/// <summary>Configuração do Turnstile: o segredo e, se houver, o hostname do SPA que o token precisa trazer.</summary>
public sealed record OpcoesTurnstile(string Segredo, string? Hostname)
{
    public static readonly Uri Siteverify = new("https://challenges.cloudflare.com/turnstile/v0/siteverify");
}

/// <summary>
/// Confere o token no <c>siteverify</c> do Cloudflare (RNF-SEG-10): aprovado só com <c>success</c>, a ação da rota
/// (<see cref="AcoesVerificacaoHumana"/>: cadastro ou lista de espera) e o hostname esperado. Sem resposta, erro HTTP ou
/// JSON estranho → indisponível, e o envio é recusado (spec do F9, decisão 5c).
/// </summary>
public sealed partial class VerificadorTurnstile : IVerificacaoHumana
{
    private readonly HttpClient _http;
    private readonly OpcoesTurnstile _opcoes;
    private readonly ILogger<VerificadorTurnstile> _logger;

    public VerificadorTurnstile(HttpClient http, OpcoesTurnstile opcoes, ILogger<VerificadorTurnstile> logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(logger);
        _http = http;
        _opcoes = opcoes;
        _logger = logger;
    }

    public async Task<ResultadoVerificacaoHumana> VerificarAsync(
        string? token, string? ip, string acao, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acao);
        if (string.IsNullOrWhiteSpace(token))
            return ResultadoVerificacaoHumana.Recusada;

        var campos = new Dictionary<string, string> { ["secret"] = _opcoes.Segredo, ["response"] = token };
        if (!string.IsNullOrEmpty(ip))
            campos["remoteip"] = ip;

        JsonElement corpo;
        try
        {
            using var conteudo = new FormUrlEncodedContent(campos);
            using var resposta = await _http.PostAsync(OpcoesTurnstile.Siteverify, conteudo, cancellationToken);
            if (!resposta.IsSuccessStatusCode)
            {
                Indisponivel(_logger, (int)resposta.StatusCode);
                return ResultadoVerificacaoHumana.Indisponivel;
            }

            corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        }
        // Timeout do HttpClient, rede ou JSON inválido; o cancelamento da própria requisição sobe.
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            Falhou(_logger, ex);
            return ResultadoVerificacaoHumana.Indisponivel;
        }

        // Segredo errado ou ausente: o problema é nosso, não de quem se cadastra (e precisa aparecer no log).
        if (CodigosDeErro(corpo).Any(c => c is "invalid-input-secret" or "missing-input-secret"))
        {
            SegredoRecusado(_logger);
            return ResultadoVerificacaoHumana.Indisponivel;
        }

        var aprovado = corpo.ValueKind == JsonValueKind.Object
            && corpo.TryGetProperty("success", out var sucesso) && sucesso.ValueKind == JsonValueKind.True
            && Texto(corpo, "action") == acao
            && (_opcoes.Hostname is null || string.Equals(Texto(corpo, "hostname"), _opcoes.Hostname, StringComparison.OrdinalIgnoreCase));
        return aprovado ? ResultadoVerificacaoHumana.Aprovada : ResultadoVerificacaoHumana.Recusada;
    }

    private static IEnumerable<string> CodigosDeErro(JsonElement corpo) =>
        corpo.ValueKind == JsonValueKind.Object && corpo.TryGetProperty("error-codes", out var codigos) && codigos.ValueKind == JsonValueKind.Array
            ? codigos.EnumerateArray().Where(c => c.ValueKind == JsonValueKind.String).Select(c => c.GetString()!)
            : [];

    private static string? Texto(JsonElement corpo, string nome) =>
        corpo.TryGetProperty(nome, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;

    [LoggerMessage(EventId = 310, Level = LogLevel.Warning, Message = "Turnstile respondeu {Status}: envio recusado (cadastro ou lista de espera).")]
    private static partial void Indisponivel(ILogger logger, int status);

    [LoggerMessage(EventId = 311, Level = LogLevel.Warning, Message = "Turnstile não respondeu: envio recusado (cadastro ou lista de espera).")]
    private static partial void Falhou(ILogger logger, Exception erro);

    [LoggerMessage(EventId = 312, Level = LogLevel.Error,
        Message = "Turnstile recusou o segredo do servidor (Turnstile__Segredo errado ou revogado): todo cadastro e toda lista de espera são recusados.")]
    private static partial void SegredoRecusado(ILogger logger);
}

/// <summary>Sem segredo configurado (dev, testes, E2E): aprova sem chamar o Cloudflare (RNF-SEG-10).</summary>
public sealed class VerificacaoHumanaDesligada : IVerificacaoHumana
{
    public Task<ResultadoVerificacaoHumana> VerificarAsync(
        string? token, string? ip, string acao, CancellationToken cancellationToken = default) =>
        Task.FromResult(ResultadoVerificacaoHumana.Aprovada);
}
