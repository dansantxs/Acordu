using System.Reflection;
using NetArchTest.Rules;

namespace Acordu.ArchitectureTests;

/// <summary>Regra de dependência: tudo aponta para dentro, em direção ao Domain.</summary>
public sealed class DependenciaEntreCamadasTests
{
    [Fact]
    public void Domain_NaoDependeDeOutrasCamadasNemDeFrameworks() =>
        NaoDeveDepender(Camadas.Domain,
            "Acordu.Application", "Acordu.Infrastructure", "Acordu.Api",
            "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Microsoft.Extensions", "Npgsql");

    [Fact]
    public void Application_NaoDependeDeInfrastructureNemDeApi() =>
        NaoDeveDepender(Camadas.Application,
            "Acordu.Infrastructure", "Acordu.Api",
            "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql");

    [Fact]
    public void Infrastructure_NaoDependeDaApi() =>
        NaoDeveDepender(Camadas.Infrastructure, "Acordu.Api");

    private static void NaoDeveDepender(Assembly assembly, params string[] proibidos)
    {
        var resultado = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(proibidos)
            .GetResult();

        resultado.IsSuccessful.Should().BeTrue(
            "tipos de {0} não podem depender de {1}; violações: {2}",
            assembly.GetName().Name,
            string.Join(", ", proibidos),
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }
}
