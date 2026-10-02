using System.Reflection;

namespace Acordu.ArchitectureTests;

/// <summary>
/// IgnoreQueryFilters desliga os isolamentos por TenantId e ClienteId (ADR 0002).
/// Só a Infrastructure pode usá-lo, em pontos revisados.
/// </summary>
public sealed class FiltrosGlobaisTests
{
    public static TheoryData<string> CamadasForaDaInfrastructure => new() { "Domain", "Application", "Api" };

    [Theory]
    [MemberData(nameof(CamadasForaDaInfrastructure))]
    public void IgnoreQueryFilters_SoNaInfrastructure(string camada)
    {
        var chamadas = Il.Chamadas(AssemblyDa(camada), alvo =>
            alvo.Name == "IgnoreQueryFilters"
            && alvo.DeclaringType.FullName == "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions");

        chamadas.Should().BeEmpty("IgnoreQueryFilters fora da Infrastructure pode vazar dados entre tenants ou clientes");
    }

    private static Assembly AssemblyDa(string camada) => camada switch
    {
        "Domain" => Camadas.Domain,
        "Application" => Camadas.Application,
        "Api" => Camadas.Api,
        _ => throw new ArgumentOutOfRangeException(nameof(camada), camada, null),
    };
}
