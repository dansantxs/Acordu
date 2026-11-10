using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Acordu.Api.Operacao;

/// <summary>
/// Log estruturado (RNF-OPS-01, ADR 0012): uma linha JSON por entrada, nos campos que o Cloud Logging lê do stdout
/// (<c>severity</c>, <c>message</c>, <c>time</c>) e no formato que o Error Reporting agrupa, sem SDK: erro com a pilha na
/// <c>message</c>, <c>@type</c> de evento de erro e <c>serviceContext</c>. Dos escopos, só os campos de
/// <see cref="CamposDoContexto"/> entram (rota modelo, método e identificadores): o caminho real, a query, o corpo e os
/// cabeçalhos ficam de fora (segurança §6).
/// </summary>
internal sealed class FormatadorLogJson(string versao, string servico = FormatadorLogJson.ServicoApi) : ConsoleFormatter(Nome)
{
    public const string Nome = "acordu-json";
    public const string ServicoApi = "acordu-api";
    private const string TipoEventoErro = "type.googleapis.com/google.devtools.clouderrorreporting.v1beta1.ReportedErrorEvent";

    /// <summary>Únicos campos de escopo que vão para o log; o resto (caminho, ids de conexão do ASP.NET) é descartado.</summary>
    internal static readonly IReadOnlySet<string> CamposDoContexto =
        new HashSet<string>(StringComparer.Ordinal) { "rota", "metodo", "tenantId", "usuarioId" };

    // Log não é HTML: acentos ficam legíveis em vez de é.
    private static readonly JsonWriterOptions OpcoesEscrita = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        ArgumentNullException.ThrowIfNull(textWriter);
        var mensagem = logEntry.Formatter(logEntry.State, logEntry.Exception);
        var excecao = logEntry.Exception;
        if (string.IsNullOrEmpty(mensagem) && excecao is null)
            return;

        var erro = logEntry.LogLevel >= LogLevel.Error;
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer, OpcoesEscrita))
        {
            json.WriteStartObject();
            json.WriteString("severity", Severidade(logEntry.LogLevel));
            json.WriteString("message", excecao is null ? mensagem : $"{mensagem}\n{excecao}");
            json.WriteString("time", DateTimeOffset.UtcNow);
            json.WriteString("categoria", logEntry.Category);
            json.WriteNumber("eventId", logEntry.EventId.Id);
            if (Activity.Current is { } atividade && atividade.TraceId != default)
                json.WriteString("traceId", atividade.TraceId.ToHexString());

            var contexto = Contexto(scopeProvider);
            if (contexto.Count > 0)
            {
                json.WriteStartObject("contexto");
                foreach (var (chave, valor) in contexto)
                    json.WriteString(chave, valor);
                json.WriteEndObject();
            }

            if (erro)
            {
                if (excecao is not null)
                    json.WriteString("@type", TipoEventoErro);
                json.WriteStartObject("serviceContext");
                json.WriteString("service", servico);
                json.WriteString("version", versao);
                json.WriteEndObject();
            }

            json.WriteEndObject();
        }

        textWriter.WriteLine(Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    internal static string Severidade(LogLevel nivel) => nivel switch
    {
        LogLevel.Trace or LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARNING",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRITICAL",
        _ => "DEFAULT",
    };

    private static SortedDictionary<string, string> Contexto(IExternalScopeProvider? escopos)
    {
        var contexto = new SortedDictionary<string, string>(StringComparer.Ordinal);
        escopos?.ForEachScope(
            static (escopo, destino) =>
            {
                if (escopo is not IEnumerable<KeyValuePair<string, object?>> pares)
                    return;
                foreach (var (chave, valor) in pares)
                {
                    if (CamposDoContexto.Contains(chave) && valor is not null)
                        destino[chave] = Convert.ToString(valor, System.Globalization.CultureInfo.InvariantCulture) ?? "";
                }
            },
            contexto);
        return contexto;
    }
}
