namespace Acordu.ArchitectureTests;

/// <summary>
/// O portal tem DTOs próprios, sem campos internos (ADR 0002, RNF-SEG-03).
/// Nenhum tipo de Acordu.Application.Portal pode usar um DTO interno.
/// </summary>
public sealed class PortalTests
{
    private const string NamespacePortal = "Acordu.Application.Portal";

    [Fact]
    public void Portal_NaoUsaDtosInternos()
    {
        var violacoes = Il.TiposReferenciados(Camadas.Application, NamespacePortal)
            .Where(par =>
                par.Referenciado.Namespace.StartsWith("Acordu.Application", StringComparison.Ordinal)
                && !par.Referenciado.Namespace.StartsWith(NamespacePortal, StringComparison.Ordinal)
                && par.Referenciado.Name.EndsWith("Dto", StringComparison.Ordinal))
            .Select(par => $"{par.Origem} → {par.Referenciado.FullName}")
            .Distinct()
            .ToList();

        violacoes.Should().BeEmpty("o portal nunca reaproveita DTO interno, mesmo com campos nulos");
    }
}
