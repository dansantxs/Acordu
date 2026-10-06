using Acordu.Infrastructure.Persistence.Outbox;

namespace Acordu.Api.Composicao;

/// <summary>
/// Filtro das rotas de negócio: depois do caso de uso (já commitado) e antes de escrever a resposta, entrega as mensagens
/// de outbox que esta requisição gravou. Não depende de trabalho depois da resposta, para valer também no plano B
/// serverless, onde a CPU é estrangulada após a resposta (ADR 0006). Falha aqui não muda a resposta: a mensagem fica na fila e o job <c>tick</c> tenta de novo.
/// </summary>
internal static partial class DespachoOutbox
{
    public static async ValueTask<object?> FiltrarAsync(EndpointFilterInvocationContext contexto, EndpointFilterDelegate proximo)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(proximo);
        var resultado = await proximo(contexto);

        var http = contexto.HttpContext;
        var ids = http.RequestServices.GetRequiredService<MensagensDaRequisicao>().Retirar();
        if (ids.Count == 0)
            return resultado;

        try
        {
            await http.RequestServices.GetRequiredService<ProcessadorOutbox>().ProcessarAsync(ids, http.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DespachoOutbox));
            FalhaNoDespacho(logger, ids.Count, ex);
        }

        return resultado;
    }

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Falha ao despachar {Quantidade} mensagem(ns) da outbox; o job tick tenta de novo.")]
    private static partial void FalhaNoDespacho(ILogger logger, int quantidade, Exception excecao);
}
