using Acordu.Domain.Common;

namespace Acordu.Domain.ValueObjects;

/// <summary>
/// Registro da solução de uma solicitação. <see cref="Texto"/> é obrigatório; problema e causa
/// são opcionais e alimentam a busca de solicitações semelhantes (RF-CON-02).
/// </summary>
public sealed record Solucao
{
    public Solucao(string texto, string? problema = null, string? causa = null)
    {
        Texto = Common.Texto.Obrigatorio(texto, LimitesTexto.SolucaoTexto, nameof(texto));
        Problema = Common.Texto.Opcional(problema, LimitesTexto.SolucaoProblemaCausa, nameof(problema));
        Causa = Common.Texto.Opcional(causa, LimitesTexto.SolucaoProblemaCausa, nameof(causa));
    }

    public string Texto { get; }

    public string? Problema { get; }

    public string? Causa { get; }
}
