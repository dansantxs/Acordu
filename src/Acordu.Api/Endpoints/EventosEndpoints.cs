using System.Text.Json;
using Acordu.Application.Abstractions;
using Acordu.Infrastructure.TempoReal;

namespace Acordu.Api.Endpoints;

/// <summary>
/// Tempo real por SSE (contrato §11e, ADR 0009): sinais de mudança do tenant para a equipe. Só "tipo e Id": o front
/// invalida o cache e busca de novo pela API, que aplica as permissões. Comentário de vida a cada 25 s.
/// </summary>
internal static class EventosEndpoints
{
    public static readonly TimeSpan IntervaloVida = TimeSpan.FromSeconds(25);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapEventos(this RouteGroupBuilder interno) =>
        interno.MapGet("/eventos", async (HttpContext http, ITenantContext contexto, DifusorSinais difusor, CancellationToken cancellationToken) =>
        {
            var resposta = http.Response;
            resposta.Headers.ContentType = "text/event-stream";
            resposta.Headers.CacheControl = "no-cache";
            resposta.Headers["X-Accel-Buffering"] = "no";
            using var assinatura = difusor.Assinar(contexto.TenantId!.Value);

            // Tudo dentro do try, inclusive a primeira escrita: a aba que recarrega ou fecha logo depois de conectar
            // cancela a requisição já no `retry`, e isso é fim normal da conexão, não erro.
            try
            {
                await resposta.WriteAsync("retry: 5000\n\n", cancellationToken);
                await resposta.Body.FlushAsync(cancellationToken);

                while (!cancellationToken.IsCancellationRequested)
                {
                    using var espera = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    espera.CancelAfter(IntervaloVida);
                    try
                    {
                        var sinal = await assinatura.Leitor.ReadAsync(espera.Token);
                        await resposta.WriteAsync($"event: mudanca\ndata: {JsonSerializer.Serialize(sinal, Json)}\n\n", cancellationToken);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        await resposta.WriteAsync(": vivo\n\n", cancellationToken);
                    }

                    await resposta.Body.FlushAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cliente fechou a conexão.
            }
        })
            .WithTags("Tempo real")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream");
}
