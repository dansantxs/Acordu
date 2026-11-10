namespace Acordu.Api.Jobs;

/// <summary>Um passo do job diário: nome (para o log) e o que ele faz, devolvendo quantos itens tratou.</summary>
internal sealed record PassoDiario(string Nome, Func<CancellationToken, Task<int>> Executar);

/// <summary>Contagem de cada passo, na ordem, quantos passos falharam e quais (para o aviso em #alertas, F13-13).</summary>
internal sealed record ResultadoDiario(IReadOnlyList<int> Contagens, int Falhas, IReadOnlyList<string> PassosFalhos);

/// <summary>
/// Roda os passos do job diário em sequência (ADR 0010 §5). Um passo que falha é logado, conta 0 e entra em
/// <c>Falhas</c>, sem impedir os seguintes; o cancelamento da requisição interrompe tudo.
/// </summary>
internal static partial class PassosDiario
{
    public static async Task<ResultadoDiario> ExecutarAsync(
        IReadOnlyList<PassoDiario> passos, ILogger logger, CancellationToken cancellationToken)
    {
        var contagens = new int[passos.Count];
        var falhas = 0;
        var falhos = new List<string>();
        for (var i = 0; i < passos.Count; i++)
        {
            try
            {
                contagens[i] = await passos[i].Executar(cancellationToken);
            }
            // Só o cancelamento da própria requisição interrompe; outro (timeout de comando) conta como falha do passo.
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                falhas++;
                falhos.Add(passos[i].Nome);
                PassoFalhou(logger, ex, passos[i].Nome);
            }
        }

        return new ResultadoDiario(contagens, falhas, falhos);
    }

    [LoggerMessage(EventId = 301, Level = LogLevel.Error, Message = "Job diário: o passo '{Passo}' falhou.")]
    private static partial void PassoFalhou(ILogger logger, Exception erro, string passo);
}
