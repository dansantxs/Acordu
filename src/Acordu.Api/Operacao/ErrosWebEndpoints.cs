using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Acordu.Api.Erros;
using Acordu.Api.Seguranca;
using Acordu.Application.Abstractions.Operacao;
using Acordu.Application.Common;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace Acordu.Api.Operacao;

/// <summary>Erro do app no navegador (contrato §16): o que o navegador sabe dele, sem dado pessoal.</summary>
internal sealed record ErroWebRequest(string? Mensagem, string? Pilha, string? Rota, string? Versao, string? Navegador);

/// <summary>
/// <c>POST /api/v1/erros-web</c> (spec do F13, F13d; RNF-OPS-03): pública, com limite por IP e corpo de até 16 KB. O erro
/// vira uma linha <c>ERROR</c> do serviço <c>acordu-web</c> (Error Reporting) e passa pelos avisos de #alertas, com a
/// impressão digital da mensagem sem números, do primeiro quadro sem o hash do arquivo e da rota modelo. Antes de tudo, o
/// texto é limpo: e-mails, ids e números longos saem, a rota vira modelo e a query some.
/// </summary>
internal static partial class ErrosWebEndpoints
{
    public const int LimiteCorpo = 16 * 1024;
    public const string Servico = "acordu-web";

    private const int LimiteMensagem = 500;
    private const int LimitePilha = 4000;
    private const int LimiteRota = 200;
    private const int LimiteNavegador = 200;

    public static void MapErrosWeb(this RouteGroupBuilder api) =>
        api.MapPost("/erros-web", RegistrarAsync)
            .AllowAnonymous()
            .RequireRateLimiting(ComposicaoSeguranca.LimiteErrosWeb)
            .WithTags("Operação")
            .Accepts<ErroWebRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<IResult> RegistrarAsync(
        HttpContext http, IOptions<JsonOptions> json, ILoggerFactory logs, CancellationToken cancellationToken)
    {
        if (http.Request.ContentLength > LimiteCorpo || await LerAsync(http.Request.Body, cancellationToken) is not { } corpo)
            return GrandeDemais();

        ErroWebRequest? pedido;
        try
        {
            pedido = JsonSerializer.Deserialize<ErroWebRequest>(corpo, json.Value.SerializerOptions);
        }
        catch (JsonException)
        {
            pedido = null;
        }

        var mensagem = Limpar(pedido?.Mensagem, LimiteMensagem);
        if (mensagem.Length == 0)
            throw ErroDeCampo.Criar("mensagem", "A mensagem do erro é obrigatória.");

        var pilha = Limpar(pedido?.Pilha, LimitePilha);
        var rota = RotaModelo(pedido?.Rota);
        var versao = pedido?.Versao is { } v && Versao().IsMatch(v) ? v : "desconhecida";
        var navegador = Cortar(pedido?.Navegador?.Trim() ?? "", LimiteNavegador);

        var logger = logs.CreateLogger(typeof(ErrosWebEndpoints));
        using (logger.BeginScope(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [FormatadorLogJson.EscopoServico] = Servico,
            [FormatadorLogJson.EscopoPilha] = pilha.Length == 0 ? null : pilha,
            ["rota"] = rota,
        }))
        {
            ErroDoApp(logger, mensagem, versao, navegador);
        }

        await AvisarAsync(http, new ErroInesperado(Servico, Cortar(SemNumeros(mensagem), 300), PrimeiroQuadro(pilha), rota, versao), logger);
        return Results.NoContent();
    }

    /// <summary>Lê até o limite; passou dele (corpo sem tamanho declarado), <c>null</c>.</summary>
    private static async Task<byte[]?> LerAsync(Stream corpo, CancellationToken cancellationToken)
    {
        var buffer = new byte[LimiteCorpo + 1];
        var lidos = 0;
        int lido;
        while (lidos < buffer.Length && (lido = await corpo.ReadAsync(buffer.AsMemory(lidos), cancellationToken)) > 0)
            lidos += lido;
        return lidos > LimiteCorpo ? null : buffer[..lidos];
    }

    private static IResult GrandeDemais() => Results.Problem(
        statusCode: StatusCodes.Status413PayloadTooLarge,
        type: ManipuladorExcecoes.BaseTipos + "corpo-grande-demais",
        title: "Corpo acima do limite",
        detail: "O relato de erro passa de 16 KB.");

    private static async Task AvisarAsync(HttpContext http, ErroInesperado erro, ILogger logger)
    {
        if (http.RequestServices.GetService<IAvisosDeErro>() is not { } avisos)
            return;
        using var prazo = new CancellationTokenSource(ManipuladorExcecoes.PrazoDoAviso);
        try
        {
            await avisos.AvisarAsync(erro, prazo.Token).WaitAsync(prazo.Token);
        }
        catch (OperationCanceledException)
        {
            AvisoAtrasado(logger);
        }
    }

    /// <summary>Tira e-mails, ids (GUID) e números longos, e a query das URLs; corta no limite.</summary>
    internal static string Limpar(string? texto, int limite)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return "";
        var limpo = QueryDeUrl().Replace(texto.Trim(), "");
        limpo = Email().Replace(limpo, "[email]");
        limpo = Identificador().Replace(limpo, ":id");
        limpo = NumeroLongo().Replace(limpo, "#");
        return Cortar(limpo, limite);
    }

    /// <summary><c>/clientes/7c1f…/contratos?aba=2</c> → <c>/clientes/:id/contratos</c>; fora do padrão, <c>(sem rota)</c>.</summary>
    internal static string RotaModelo(string? rota)
    {
        if (string.IsNullOrWhiteSpace(rota) || !rota.StartsWith('/') || rota.StartsWith("//", StringComparison.Ordinal))
            return "(sem rota)";
        var caminho = rota.Split('?', '#')[0];
        var partes = caminho.Split('/').Select(p => Identificador().IsMatch(p) || (p.Length > 0 && p.All(char.IsAsciiDigit)) || p.Length > 40 ? ":id" : p);
        return Cortar(string.Join('/', partes), LimiteRota);
    }

    /// <summary>Números viram <c>#</c>: o mesmo erro com outro valor tem a mesma impressão (F13-18).</summary>
    internal static string SemNumeros(string mensagem) => Digitos().Replace(mensagem, "#");

    /// <summary>
    /// Primeiro quadro da pilha como <c>funcao (arquivo.js)</c>, sem o hash do build nem linha e coluna: estável entre
    /// versões. Lê o formato do Chrome (<c>at f (url:1:2)</c>) e o do Firefox e Safari (<c>f@url:1:2</c>).
    /// </summary>
    internal static string? PrimeiroQuadro(string pilha)
    {
        foreach (var linha in pilha.Split('\n'))
        {
            var quadro = QuadroChrome().Match(linha) is { Success: true } chrome
                ? chrome
                : QuadroFirefox().Match(linha) is { Success: true } firefox ? firefox : null;
            if (quadro is null)
                continue;
            var funcao = quadro.Groups["funcao"].Value is { Length: > 0 } f ? f : "(anônima)";
            var arquivo = HashDoArquivo().Replace(quadro.Groups["arquivo"].Value, "");
            return Cortar($"{funcao} ({arquivo})", 300);
        }

        return null;
    }

    private static string Cortar(string texto, int limite) => texto.Length <= limite ? texto : texto[..limite];

    [GeneratedRegex(@"\?[^\s)]*")]
    private static partial Regex QueryDeUrl();

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex Email();

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")]
    private static partial Regex Identificador();

    [GeneratedRegex(@"\d{4,}")]
    private static partial Regex NumeroLongo();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digitos();

    [GeneratedRegex(@"^[A-Za-z0-9.\-]{1,40}$")]
    private static partial Regex Versao();

    [GeneratedRegex(@"^\s*at (?:(?<funcao>[^\s(]+) \()?(?:[a-z]+://[^/\s]+)?/?(?<arquivo>[^\s:)]+?)(?::\d+){0,2}\)?\s*$")]
    private static partial Regex QuadroChrome();

    [GeneratedRegex(@"^\s*(?<funcao>[^@\s]*)@(?:[a-z]+://[^/\s]+)?/?(?<arquivo>[^\s:]+?)(?::\d+){0,2}\s*$")]
    private static partial Regex QuadroFirefox();

    /// <summary><c>assets/index-B3x9_aZk.js</c> → <c>assets/index.js</c>.</summary>
    [GeneratedRegex(@"-[A-Za-z0-9_]{8,}(?=\.[a-z]+$)")]
    private static partial Regex HashDoArquivo();

    [LoggerMessage(EventId = 610, Level = LogLevel.Error, Message = "Erro no app: {Mensagem} (versão {Versao}, {Navegador}).")]
    private static partial void ErroDoApp(ILogger logger, string mensagem, string versao, string navegador);

    [LoggerMessage(EventId = 611, Level = LogLevel.Warning, Message = "Aviso do erro do app passou do prazo; a resposta saiu sem ele.")]
    private static partial void AvisoAtrasado(ILogger logger);
}
