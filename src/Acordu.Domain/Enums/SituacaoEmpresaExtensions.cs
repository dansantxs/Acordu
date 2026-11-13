namespace Acordu.Domain.Enums;

/// <summary>
/// Fonte única do critério "só leitura" (RF-EMP-02): VENCIDA e CANCELADA. A lista <see cref="SituacoesSomenteLeitura"/>
/// serve também dentro de consulta do EF (<c>SituacoesSomenteLeitura.Contains(t.Situacao)</c> vira SQL), onde o método de
/// extensão não traduz.
/// </summary>
public static class SituacaoEmpresaExtensions
{
    /// <summary>As situações em que a empresa lê e não grava.</summary>
    public static readonly IReadOnlyCollection<SituacaoEmpresa> SituacoesSomenteLeitura =
        [SituacaoEmpresa.Vencida, SituacaoEmpresa.Cancelada];

    /// <summary>VENCIDA ou CANCELADA.</summary>
    public static bool SomenteLeitura(this SituacaoEmpresa situacao) => SituacoesSomenteLeitura.Contains(situacao);
}
