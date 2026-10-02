using System.Reflection;

namespace Acordu.ArchitectureTests;

/// <summary>Assemblies de cada camada, localizados por um tipo âncora.</summary>
internal static class Camadas
{
    public static readonly Assembly Domain = typeof(global::Acordu.Domain.AssemblyReference).Assembly;
    public static readonly Assembly Application = typeof(global::Acordu.Application.AssemblyReference).Assembly;
    public static readonly Assembly Infrastructure = typeof(global::Acordu.Infrastructure.AssemblyReference).Assembly;
    public static readonly Assembly Api = typeof(global::Program).Assembly;
}
