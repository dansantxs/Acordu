namespace Acordu.Application.Abstractions.Operacao;

/// <summary>Canal da operação da Nebrys (ADR 0012): cada um com o seu webhook.</summary>
public enum CanalOperacao
{
    /// <summary>Erros, jobs, queda, deploy e backup que falharam.</summary>
    Alertas,

    /// <summary>Deploy ok, heartbeat do backup e resumo do diário.</summary>
    Deploy,

    /// <summary>Sugestões das equipes (RF-SUG-01).</summary>
    Sugestoes,
}

/// <summary>
/// Mensagem à operação da Nebrys (RNF-OPS-02, ADR 0012). Nunca leva dado de cliente; menções sempre desligadas. A
/// falha não sobe: devolve <c>false</c>, e quem chama decide se tenta de novo.
/// </summary>
public interface IMensageiroOperacao
{
    /// <summary>Se o canal tem webhook (sem ele, nada sai: desenvolvimento e testes).</summary>
    bool Configurado(CanalOperacao canal);

    Task<bool> EnviarAsync(CanalOperacao canal, string texto, CancellationToken cancellationToken = default);
}
