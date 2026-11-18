using System.Security.Cryptography;
using System.Text;
using Acordu.Application.Empresas;
using Acordu.Application.Notificacoes;
using Acordu.Application.Operacao;
using Acordu.Application.Recorrencias;
using Acordu.Application.Solicitacoes;
using Acordu.Application.Sugestoes;
using Acordu.Infrastructure.Agendamento;
using Acordu.Infrastructure.Operacao;
using Acordu.Infrastructure.Persistence.Auditoria;
using Acordu.Infrastructure.Persistence.Atividades;
using Acordu.Infrastructure.Persistence.Cadastros;
using Acordu.Infrastructure.Persistence.Notificacoes;
using Acordu.Infrastructure.Persistence.Outbox;

namespace Acordu.Api.Jobs;

/// <summary>
/// Jobs agendados como endpoints internos (ADR 0010 §5): chamados pelo Cloud Scheduler, pela borda, com
/// <c>X-Job-Token</c>; idempotentes e por todos os tenants. Sem token configurado, ou com token errado, respondem 404
/// (não revelam a rota).
/// </summary>
internal static partial class JobsEndpoints
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
        jobs.MapPost("/tick", async (
            ProcessadorOutbox processador,
            ExecutorPorTenant executor,
            AvisosDosJobs avisos,
            ComandosSugestao sugestoes,
            ILoggerFactory logs,
            CancellationToken cancellationToken) =>
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
            // Cronômetros esquecidos (F12-18): aviso ao dono, uma vez por cronômetro.
            var esquecidos = await executor.ExecutarAsync(
                "cronometros-esquecidos",
                (servicos, ct) => servicos.GetRequiredService<AvisosCronometroEsquecido>().ExecutarAsync(ct),
                cancellationToken);
            // Sugestões que não saíram em #sugestoes na hora (F13-29). Como os outros passos: a falha conta e não derruba o tick.
            var sugestoesReenviadas = 0;
            var falhaSugestoes = 0;
            try
            {
                sugestoesReenviadas = await sugestoes.ReenviarPendentesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                falhaSugestoes = 1;
                ReenvioFalhou(logs.CreateLogger(typeof(JobsEndpoints)), ex);
            }

            // Falha por tenant em algum passo: #alertas, uma vez por passo e por dia (F13-13).
            await avisos.FalhasAsync(
                "tick",
                [
                    new("recorrencias", recorrencias.Falhas), new("alertas-sla", alertas.Falhas),
                    new("cronometros-esquecidos", esquecidos.Falhas), new("sugestoes-pendentes", falhaSugestoes),
                ],
                cancellationToken);
            return Results.Ok(new
            {
                tarefasRecorrentes = recorrencias.Tratados,
                outboxProcessadas = total,
                alertasSla = alertas.Tratados,
                cronometrosEsquecidos = esquecidos.Tratados,
                sugestoesReenviadas,
                falhas = recorrencias.Falhas + alertas.Falhas + esquecidos.Falhas + falhaSugestoes,
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
    /// O disparo diário do Scheduler (ADR 0010 §5; o plano gratuito tem 3 jobs): vencimento dos testes (F14a; primeiro,
    /// para a recém-vencida já ficar fora dos passos seguintes), avisos de situação aos Donos (F14j; logo depois, para o
    /// aviso do vencimento sair no mesmo diário), fechamento automático, expurgo da auditoria, expurgo das notificações e
    /// do feed, expurgo dos cadastros abandonados (RF-AUT-07), das empresas só leitura há o prazo (RF-EMP-03, F14i), das
    /// ocorrências de erro e das marcas de operação (ADR 0012), em sequência; no fim, os avisos do F13c. Um passo que
    /// falha é logado, entra em <c>falhas</c> e não impede os seguintes; os endpoints individuais continuam para disparo manual.
    /// </summary>
    private static void MapDiario(RouteGroupBuilder jobs) =>
        jobs.MapPost("/diario", async (
            ExecutorPorTenant executor,
            AvisosDeSituacao situacao,
            ExpurgoAuditoria auditoria,
            ExpurgoNotificacoes notificacoes,
            ExpurgoAtividades atividades,
            ExpurgoCadastros cadastros,
            ExpurgoEmpresas empresas,
            ExpurgoOcorrenciasErro ocorrencias,
            MarcasDeOperacao marcas,
            AvisosDosJobs avisos,
            ILoggerFactory logs,
            CancellationToken cancellationToken) =>
        {
            var falhasVencimento = 0;
            var falhasSituacao = 0;
            var falhasFechamento = 0;
            var falhasCadastros = 0;
            var falhasEmpresas = 0;
            var resultado = await PassosDiario.ExecutarAsync(
                [
                    // Cada empresa no próprio escopo e contexto (F14-09): a falha de uma conta e não para as outras.
                    new("vencimento-testes", async ct =>
                    {
                        var vencimento = await executor.ExecutarAsync(
                            "vencimento-testes",
                            (servicos, c) => servicos.GetRequiredService<VencimentoTestes>().ExecutarAsync(c),
                            ct);
                        falhasVencimento = vencimento.Falhas;
                        return vencimento.Tratados;
                    }),
                    // E-mails aos Donos antes do fim do teste, no vencimento e antes do expurgo (RF-EMP-04, F14j), com laço
                    // próprio: as empresas só leitura também recebem.
                    new("avisos-situacao", async ct =>
                    {
                        var envio = await situacao.ExecutarAsync(ct);
                        falhasSituacao = envio.Falhas;
                        return envio.Avisadas;
                    }),
                    new("fechamento-automatico", async ct =>
                    {
                        var fechamento = await executor.ExecutarAsync(
                            "fechamento-automatico",
                            (servicos, c) => servicos.GetRequiredService<FechamentoAutomatico>().ExecutarAsync(c),
                            ct);
                        falhasFechamento = fechamento.Falhas;
                        return fechamento.Tratados;
                    }),
                    new("expurgo-auditoria", auditoria.ExecutarAsync),
                    new("expurgo-notificacoes", notificacoes.ExecutarAsync),
                    new("expurgo-atividades", atividades.ExecutarAsync),
                    new("expurgo-cadastros", async ct =>
                    {
                        var expurgo = await cadastros.ExecutarAsync(ct);
                        falhasCadastros = expurgo.Falhas;
                        return expurgo.Removidos;
                    }),
                    // Empresa VENCIDA ou CANCELADA há o prazo ou mais (RF-EMP-03, F14i): cada uma na própria transação.
                    new("expurgo-empresas", async ct =>
                    {
                        var expurgo = await empresas.ExecutarAsync(ct);
                        falhasEmpresas = expurgo.Falhas;
                        return expurgo.Expurgadas;
                    }),
                    new("expurgo-ocorrencias-erro", ocorrencias.ExecutarAsync),
                    new("expurgo-marcas-operacao", marcas.ExpurgarAsync),
                ],
                logs.CreateLogger(typeof(PassosDiario)),
                cancellationToken);
            var falhas = AvisosDosJobs.PorPasso(
                resultado.PassosFalhos,
                [
                    new("vencimento-testes", falhasVencimento), new("avisos-situacao", falhasSituacao),
                    new("fechamento-automatico", falhasFechamento), new("expurgo-cadastros", falhasCadastros),
                    new("expurgo-empresas", falhasEmpresas),
                ]);
            await avisos.DiarioAsync(
                falhas,
                [
                    ("vencidas", resultado.Contagens[0]), ("avisos de situação", resultado.Contagens[1]),
                    ("fechadas", resultado.Contagens[2]), ("auditoria", resultado.Contagens[3]),
                    ("notificações", resultado.Contagens[4]), ("atividades", resultado.Contagens[5]),
                    ("cadastros", resultado.Contagens[6]), ("empresas expurgadas", resultado.Contagens[7]),
                    ("ocorrências de erro", resultado.Contagens[8]), ("marcas", resultado.Contagens[9]),
                ],
                cancellationToken);

            return Results.Ok(new
            {
                empresasVencidas = resultado.Contagens[0],
                avisosSituacao = resultado.Contagens[1],
                fechadas = resultado.Contagens[2],
                auditoriaRemovida = resultado.Contagens[3],
                notificacoesRemovidas = resultado.Contagens[4],
                atividadesRemovidas = resultado.Contagens[5],
                cadastrosRemovidos = resultado.Contagens[6],
                empresasExpurgadas = resultado.Contagens[7],
                ocorrenciasErroRemovidas = resultado.Contagens[8],
                marcasOperacaoRemovidas = resultado.Contagens[9],
                falhas = resultado.Falhas + falhasVencimento + falhasSituacao + falhasFechamento + falhasCadastros + falhasEmpresas,
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

    [LoggerMessage(EventId = 302, Level = LogLevel.Error, Message = "Tick: o reenvio das sugestões pendentes falhou.")]
    private static partial void ReenvioFalhou(ILogger logger, Exception erro);
}
