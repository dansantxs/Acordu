using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Console;

namespace Acordu.Api.Operacao;

/// <summary>
/// Formato do log (RNF-OPS-01, ADR 0012), só por variável de ambiente: <c>Logs__Formato=json</c> liga o
/// <see cref="FormatadorLogJson"/> (produção); sem ela, o console de texto de sempre (desenvolvimento e testes). A versão
/// vem de <c>App__Versao</c> (o commit, no deploy).
/// </summary>
internal static class ComposicaoLogs
{
    public const string FormatoJson = "json";

    /// <summary>Se o log sai em JSON (produção).</summary>
    public static bool EmJson(IConfiguration configuracao) =>
        string.Equals(configuracao["Logs:Formato"]?.Trim(), FormatoJson, StringComparison.OrdinalIgnoreCase);

    public static void AddLogsAcordu(this IServiceCollection services, IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        var formato = configuracao["Logs:Formato"];
        if (string.IsNullOrWhiteSpace(formato))
            return;
        if (!string.Equals(formato.Trim(), FormatoJson, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Logs__Formato aceita só '{FormatoJson}' (veio '{formato}').");

        var versao = configuracao["App:Versao"] is { Length: > 0 } v ? v.Trim() : "dev";
        services.AddLogging(logs => logs.AddConsole(opcoes => opcoes.FormatterName = FormatadorLogJson.Nome));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ConsoleFormatter, FormatadorLogJson>(_ => new FormatadorLogJson(versao)));
    }
}
