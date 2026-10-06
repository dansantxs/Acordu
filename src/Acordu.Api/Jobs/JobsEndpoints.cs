using System.Security.Cryptography;
using System.Text;
using Acordu.Infrastructure.Persistence.Outbox;

namespace Acordu.Api.Jobs;

/// <summary>
/// Jobs agendados como endpoints internos (ADR 0005 §6): chamados por cron externo com <c>X-Job-Token</c>, idempotentes
/// e por todos os tenants. Sem token configurado, ou com token errado, respondem 404 (não revelam a rota).
/// </summary>
internal static class JobsEndpoints
{
    public const string CabecalhoToken = "X-Job-Token";

    /// <summary>Teto de mensagens por chamada do tick (lotes de 100).</summary>
    private const int LotesPorTick = 10;

    public static void MapJobs(this IEndpointRouteBuilder app, IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        var jobs = app.MapGroup("/internal/jobs")
            .ExcludeFromDescription()
            .AddEndpointFilter((contexto, proximo) => TokenValido(contexto.HttpContext, configuracao["Jobs:Token"])
                ? proximo(contexto)
                : ValueTask.FromResult<object?>(Results.NotFound()));

        // Varre a outbox (o que não saiu no fim das requisições). Alertas de SLA entram aqui depois.
        jobs.MapPost("/tick", async (ProcessadorOutbox processador, CancellationToken cancellationToken) =>
        {
            var total = 0;
            for (var lote = 0; lote < LotesPorTick; lote++)
            {
                var processadas = await processador.ProcessarLoteAsync(100, cancellationToken);
                total += processadas;
                if (processadas == 0)
                    break;
            }

            return Results.Ok(new { outboxProcessadas = total });
        });
    }

    private static bool TokenValido(HttpContext http, string? esperado)
    {
        if (string.IsNullOrEmpty(esperado))
            return false;

        var recebido = http.Request.Headers[CabecalhoToken].ToString();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(recebido), Encoding.UTF8.GetBytes(esperado));
    }
}
