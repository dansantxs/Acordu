using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Acordu.Api.Operacao;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acordu.Api.FunctionalTests.Apoio;

/// <summary>Provedor de log que formata cada entrada (a partir de <c>minimo</c>, padrão Warning) com o formatador da Api, como o console faria.</summary>
internal sealed class LogsCapturados(LogLevel minimo = LogLevel.Warning) : ILoggerProvider, ISupportExternalScope
{
    private readonly LogLevel _minimo = minimo;
    private IExternalScopeProvider? _escopos;

    /// <summary>O formatador registrado na Api (definido depois do build); sem ele, nada é capturado.</summary>
    public FormatadorLogJson? Formatador { get; set; }

    public ConcurrentQueue<JsonElement> Linhas { get; } = new();

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _escopos = scopeProvider;

    public ILogger CreateLogger(string categoryName) => new Registro(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Registro(LogsCapturados dono, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => dono._escopos?.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel >= dono._minimo;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (dono.Formatador is null || !IsEnabled(logLevel))
                return;
            using var saida = new StringWriter(CultureInfo.InvariantCulture);
            dono.Formatador.Write(new LogEntry<TState>(logLevel, categoria, eventId, state, exception, formatter), dono._escopos, saida);
            if (saida.ToString() is { Length: > 0 } texto)
                dono.Linhas.Enqueue(JsonDocument.Parse(texto).RootElement.Clone());
        }
    }
}
