using Acordu.Domain.Entities;

namespace Acordu.Application.Sugestoes;

/// <summary>Sugestão pronta para o aviso: com o nome da empresa (que a operação lê) e sem nada da pessoa.</summary>
public sealed record SugestaoParaAviso(Guid Id, int Numero, string Texto, string? Tela, string Empresa);

/// <summary>Gravação e leitura das sugestões, globais (RF-SUG-01). Nenhuma leitura chega a uma rota.</summary>
public interface ISugestoes
{
    /// <summary>Grava e devolve o número dado pelo banco.</summary>
    Task<int> GravarAsync(Sugestao sugestao, CancellationToken cancellationToken = default);

    /// <summary>Quantas a pessoa mandou desde <paramref name="desde"/> (o limite por hora).</summary>
    Task<int> DaPessoaDesdeAsync(Guid pessoaId, DateTimeOffset desde, CancellationToken cancellationToken = default);

    Task<SugestaoParaAviso?> ParaAvisoAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>As que não saíram em #sugestoes, criadas até <paramref name="ate"/>, das mais antigas.</summary>
    Task<IReadOnlyList<SugestaoParaAviso>> PendentesAsync(DateTimeOffset ate, int maximo, CancellationToken cancellationToken = default);

    Task MarcarAvisadaAsync(Guid id, DateTimeOffset agora, CancellationToken cancellationToken = default);
}
