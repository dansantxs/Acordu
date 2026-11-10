namespace Acordu.Application.Abstractions.Operacao;

/// <summary>
/// Marca "já avisado" da operação, por chave (ex.: <c>job:tick:alertas-sla:2026-11-10</c>), global e fora dos tenants
/// (ADR 0012). Serve para avisar uma vez por dia mesmo com o Scheduler repetindo o disparo.
/// </summary>
public interface IMarcasDeOperacao
{
    /// <summary>Marca a chave; <c>true</c> se é a primeira vez (quem marcou avisa). Sem banco, <c>true</c>: melhor avisar a mais.</summary>
    Task<bool> MarcarAsync(string chave, CancellationToken cancellationToken = default);

    /// <summary>Desfaz a marca quando o aviso não saiu, para a próxima tentativa avisar.</summary>
    Task DesmarcarAsync(string chave, CancellationToken cancellationToken = default);
}
