using System.Buffers;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Acordu.Api.Operacao;

/// <summary>
/// Log estruturado (RNF-OPS-01, ADR 0012): uma linha JSON por entrada, nos campos que o Cloud Logging lê do stdout
/// (<c>severity</c>, <c>message</c>, <c>time</c>) e no formato que o Error Reporting agrupa, sem SDK: erro com a pilha em
/// <c>stack_trace</c>, <c>@type</c> de evento de erro e <c>serviceContext</c>. Dos escopos, só os campos de
/// <see cref="CamposDoContexto"/> entram (rota modelo, método e identificadores): o caminho real, a query, o corpo e os
/// cabeçalhos ficam de fora (segurança §6).
/// </summary>
internal sealed class FormatadorLogJson(string versao, string servico = FormatadorLogJson.ServicoApi) : ConsoleFormatter(Nome)
{
    public const string Nome = "acordu-json";
    public const string ServicoApi = "acordu-api";
    private const string TipoEventoErro = "type.googleapis.com/google.devtools.clouderrorreporting.v1beta1.ReportedErrorEvent";

    /// <summary>Únicos campos de escopo que vão para o log; o resto (caminho, ids de conexão do ASP.NET) é descartado.</summary>
    internal static readonly FrozenSet<string> CamposDoContexto =
        new[] { "rota", "metodo", "tenantId", "usuarioId" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Escopos especiais, fora do <c>contexto</c>: o erro que veio de outro serviço (o app no navegador, F13d) traz o
    /// nome dele e a pilha em texto (não há exceção .NET).
    /// </summary>
    internal const string EscopoServico = "servico";
    internal const string EscopoPilha = "pilha";
    internal const string EscopoVersao = "versao";

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
            json.WriteString("message", mensagem);
            json.WriteString("time", DateTimeOffset.UtcNow);
            json.WriteString("categoria", logEntry.Category);
            json.WriteNumber("eventId", logEntry.EventId.Id);
            if (Activity.Current is { } atividade && atividade.TraceId != default)
                json.WriteString("traceId", atividade.TraceId.ToHexString());

            var (contexto, outroServico, pilha, outraVersao) = Contexto(scopeProvider);
            if (contexto.Count > 0)
            {
                json.WriteStartObject("contexto");
                foreach (var (chave, valor) in contexto)
                    json.WriteString(chave, valor);
                json.WriteEndObject();
            }

            // A pilha num campo próprio, que o Error Reporting lê, e a mensagem limpa para o resumo do Cloud Logging.
            var textoDaPilha = excecao?.ToString() ?? pilha;
            if (textoDaPilha is not null)
                json.WriteString("stack_trace", textoDaPilha);

            if (erro)
            {
                if (textoDaPilha is not null)
                    json.WriteString("@type", TipoEventoErro);
                json.WriteStartObject("serviceContext");
                json.WriteString("service", outroServico ?? servico);
                json.WriteString("version", outraVersao ?? versao);
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

    private static (SortedDictionary<string, string> Contexto, string? Servico, string? Pilha, string? Versao) Contexto(
        IExternalScopeProvider? escopos)
    {
        var lido = new Lido();
        escopos?.ForEachScope(
            static (escopo, destino) =>
            {
                if (escopo is not IEnumerable<KeyValuePair<string, object?>> pares)
                    return;
                foreach (var (chave, valor) in pares)
                {
                    if (valor is null)
                        continue;
                    var texto = Convert.ToString(valor, System.Globalization.CultureInfo.InvariantCulture) ?? "";
                    if (chave == EscopoServico)
                        destino.Servico = texto;
                    else if (chave == EscopoPilha)
                        destino.Pilha = texto;
                    else if (chave == EscopoVersao)
                        destino.Versao = texto;
                    else if (CamposDoContexto.Contains(chave))
                        destino.Campos[chave] = texto;
                }
            },
            lido);
        return (lido.Campos, lido.Servico, lido.Pilha, lido.Versao);
    }

    private sealed class Lido
    {
        public SortedDictionary<string, string> Campos { get; } = new(StringComparer.Ordinal);

        public string? Servico { get; set; }

        public string? Pilha { get; set; }

        public string? Versao { get; set; }
    }
}
