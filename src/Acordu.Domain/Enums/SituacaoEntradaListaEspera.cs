namespace Acordu.Domain.Enums;

/// <summary>
/// Situação de uma entrada da lista de espera (RF-AUT-08, F14o; modelo de domínio §4.14), calculada pela data do convite
/// da <c>EntradaListaEspera</c>: não é gravada.
/// </summary>
public enum SituacaoEntradaListaEspera
{
    /// <summary>Chegou pela página pública e ainda não virou convite (F14-52, F14-75).</summary>
    Aguardando = 1,

    /// <summary>A Nebrys emitiu um convite de empresa a partir dela (F14-76).</summary>
    Convidada = 2,
}
