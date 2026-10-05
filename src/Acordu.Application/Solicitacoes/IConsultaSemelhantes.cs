namespace Acordu.Application.Solicitacoes;

/// <summary>
/// Solicitações resolvidas parecidas com uma solicitação (RF-CON-02, read side §8): até 5, por relevância, nunca de
/// outro tenant. Nulo quando a solicitação não existe ou não é visível no contexto.
/// </summary>
public interface IConsultaSemelhantes
{
    Task<IReadOnlyList<SolicitacaoSemelhanteDto>?> SemelhantesAsync(Guid solicitacaoId, CancellationToken cancellationToken = default);
}

/// <summary>Sugestão de solução já aplicada; <see cref="SolucaoResumo"/> são os primeiros 200 caracteres da solução.</summary>
public sealed record SolicitacaoSemelhanteDto(
    Guid Id, int Numero, string Titulo, string SolucaoResumo, DateTimeOffset ResolvidaEm, double Relevancia);
