using System.Security.Cryptography;
using System.Text;
using Acordu.Application.Notificacoes;
using Acordu.Application.Recorrencias;
using Acordu.Application.Solicitacoes;
using Acordu.Infrastructure.Agendamento;
using Acordu.Infrastructure.Persistence.Auditoria;
using Acordu.Infrastructure.Persistence.Atividades;
using Acordu.Infrastructure.Persistence.Notificacoes;
using Acordu.Infrastructure.Persistence.Outbox;

namespace Acordu.Api.Jobs;

/// <summary>
/// Jobs agendados como endpoints internos (ADR 0010 §5): chamados pelo Cloud Scheduler, pela borda, com
/// <c>X-Job-Token</c>; idempotentes e por todos os tenants. Sem token configurado, ou com token errado, respondem 404
/// (não revelam a rota).
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

        // Gera as tarefas recorrentes devidas (RF-REC-03), varre a outbox (o que não saiu no fim das requisições) e envia
        // os alertas de SLA (RF-NOT-02). As recorrências vêm antes: nenhum efeito pós-commit delas hoje, mas a ordem
        // deixa qualquer um futuro sair no mesmo tick.
        jobs.MapPost("/tick", async (ProcessadorOutbox processador, ExecutorPorTenant executor, CancellationToken cancellationToken) =>
        {
            var recorrencias = await executor.ExecutarAsync(
                "recorrencias",
                (servicos, ct) => servicos.GetRequiredService<GeracaoRecorrencias>().ExecutarAsync(ct),
                cancellationToken);

            var total = 0;
            for (var lote = 0; lote < LotesPorTick; lote++)
            {
                var processadas = await processador.ProcessarLoteAsync(100, cancellationToken);
                total += processadas;
                if (processadas == 0)
                    break;
            }

            var alertas = await executor.ExecutarAsync(
                "alertas-sla",
                (servicos, ct) => servicos.GetRequiredService<AlertasSla>().ExecutarAsync(ct),
                cancellationToken);
            return Results.Ok(new
            {
                tarefasRecorrentes = recorrencias.Tratados,
                outboxProcessadas = total,
                alertasSla = alertas.Tratados,
                falhas = recorrencias.Falhas + alertas.Falhas,
            });
        });

        // Diário: RESOLVIDAS há mais de Tenant.DiasFechamentoAutomatico dias corridos → FECHADA (RF-SOL-09).
        jobs.MapPost("/fechamento-automatico", async (ExecutorPorTenant executor, CancellationToken cancellationToken) =>
        {
            var resultado = await executor.ExecutarAsync(
                "fechamento-automatico",
                (servicos, ct) => servicos.GetRequiredService<FechamentoAutomatico>().ExecutarAsync(ct),
                cancellationToken);
            return Results.Ok(new { resultado.Tenants, fechadas = resultado.Tratados, resultado.Falhas });
        });

        MapExpurgoAuditoria(jobs);
        MapExpurgoNotificacoes(jobs);
        MapDiario(jobs);
    }

    /// <summary>
    /// O disparo diário do Scheduler (ADR 0010 §5; o plano gratuito tem 3 jobs): fechamento automático, expurgo da
    /// auditoria e expurgo das notificações e do feed, em sequência. Um passo que falha é logado, entra em <c>falhas</c>
    /// e não impede os seguintes; os endpoints individuais continuam para disparo manual.
    /// </summary>
    private static void MapDiario(RouteGroupBuilder jobs) =>
        jobs.MapPost("/diario", async (
            ExecutorPorTenant executor,
            ExpurgoAuditoria auditoria,
            ExpurgoNotificacoes notificacoes,
            ExpurgoAtividades atividades,
            ILoggerFactory logs,
            CancellationToken cancellationToken) =>
        {
            var falhasPorTenant = 0;
            var resultado = await PassosDiario.ExecutarAsync(
                [
                    new("fechamento-automatico", async ct =>
                    {
                        var fechamento = await executor.ExecutarAsync(
                            "fechamento-automatico",
                            (servicos, c) => servicos.GetRequiredService<FechamentoAutomatico>().ExecutarAsync(c),
                            ct);
                        falhasPorTenant = fechamento.Falhas;
                        return fechamento.Tratados;
                    }),
                    new("expurgo-auditoria", auditoria.ExecutarAsync),
                    new("expurgo-notificacoes", notificacoes.ExecutarAsync),
                    new("expurgo-atividades", atividades.ExecutarAsync),
                ],
                logs.CreateLogger(typeof(PassosDiario)),
                cancellationToken);
            return Results.Ok(new
            {
                fechadas = resultado.Contagens[0],
                auditoriaRemovida = resultado.Contagens[1],
                notificacoesRemovidas = resultado.Contagens[2],
                atividadesRemovidas = resultado.Contagens[3],
                falhas = resultado.Falhas + falhasPorTenant,
            });
        });

    /// <summary>Remove registros de auditoria com mais de 12 meses (segurança §8). Diário, no mesmo timer do fechamento.</summary>
    private static void MapExpurgoAuditoria(RouteGroupBuilder jobs) =>
        jobs.MapPost("/expurgo-auditoria", async (ExpurgoAuditoria expurgo, CancellationToken cancellationToken) =>
            Results.Ok(new { removidos = await expurgo.ExecutarAsync(cancellationToken) }));

    /// <summary>Remove notificações do sino e atividades do feed com mais de 90 dias. Diário, no mesmo timer.</summary>
    private static void MapExpurgoNotificacoes(RouteGroupBuilder jobs) =>
        jobs.MapPost("/expurgo-notificacoes", async (
            ExpurgoNotificacoes notificacoes, ExpurgoAtividades atividades, CancellationToken cancellationToken) =>
            Results.Ok(new
            {
                removidas = await notificacoes.ExecutarAsync(cancellationToken),
                atividadesRemovidas = await atividades.ExecutarAsync(cancellationToken),
            }));

    private static bool TokenValido(HttpContext http, string? esperado)
    {
        if (string.IsNullOrEmpty(esperado))
            return false;

        var recebido = http.Request.Headers[CabecalhoToken].ToString();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(recebido), Encoding.UTF8.GetBytes(esperado));
    }
}
