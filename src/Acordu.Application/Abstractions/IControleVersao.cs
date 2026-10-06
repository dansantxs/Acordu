using Acordu.Domain.Common;

namespace Acordu.Application.Abstractions;

/// <summary>
/// Concorrência otimista pela versão da linha (<c>xmin</c>), que o domínio não conhece (convenções §1).
/// </summary>
public interface IControleVersao
{
    /// <summary>Versão atual da raiz carregada, exposta como <c>versao</c> nas respostas.</summary>
    string Versao(EntidadeBase raiz);

    /// <summary>
    /// Exige que a raiz ainda esteja em <paramref name="versaoEsperada"/> (do <c>If-Match</c>) ao salvar; se outra
    /// requisição a alterou, o save falha com conflito (409). Nulo não exige nada.
    /// </summary>
    void Esperar(EntidadeBase raiz, string? versaoEsperada);
}
