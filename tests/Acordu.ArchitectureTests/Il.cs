using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Acordu.ArchitectureTests;

/// <summary>
/// Inspeção do IL compilado. Cobre o que o NetArchTest não enxerga: chamadas a membros
/// específicos (ex.: DateTime.UtcNow) e os tipos usados nas assinaturas.
/// </summary>
internal static class Il
{
    /// <summary>Lista "Tipo.Metodo → Alvo" para cada chamada cujo alvo satisfaz o predicado.</summary>
    public static IReadOnlyList<string> Chamadas(Assembly assembly, Func<MethodReference, bool> predicado)
    {
        using var modulo = ModuleDefinition.ReadModule(assembly.Location);
        var encontradas = new List<string>();

        foreach (var tipo in modulo.GetTypes())
        {
            foreach (var metodo in tipo.Methods.Where(m => m.HasBody))
            {
                foreach (var instrucao in metodo.Body.Instructions)
                {
                    if (instrucao.OpCode.Code is Code.Call or Code.Callvirt or Code.Newobj or Code.Ldftn
                        && instrucao.Operand is MethodReference alvo
                        && predicado(alvo))
                    {
                        encontradas.Add($"{tipo.FullName}.{metodo.Name} → {alvo.DeclaringType.FullName}.{alvo.Name}");
                    }
                }
            }
        }

        return encontradas;
    }

    /// <summary>
    /// Para cada tipo do namespace informado, lista os tipos referenciados em base, campos,
    /// propriedades e assinaturas de métodos (incluindo argumentos genéricos e arrays).
    /// </summary>
    public static IReadOnlyList<(string Origem, TypeReference Referenciado)> TiposReferenciados(
        Assembly assembly, string prefixoNamespace)
    {
        using var modulo = ModuleDefinition.ReadModule(assembly.Location);
        var pares = new List<(string, TypeReference)>();

        foreach (var tipo in modulo.GetTypes().Where(t => t.Namespace.StartsWith(prefixoNamespace, StringComparison.Ordinal)))
        {
            var usados = new List<TypeReference>();
            if (tipo.BaseType is not null) usados.Add(tipo.BaseType);
            usados.AddRange(tipo.Interfaces.Select(i => i.InterfaceType));
            usados.AddRange(tipo.Fields.Select(f => f.FieldType));
            usados.AddRange(tipo.Properties.Select(p => p.PropertyType));
            foreach (var metodo in tipo.Methods)
            {
                usados.Add(metodo.ReturnType);
                usados.AddRange(metodo.Parameters.Select(p => p.ParameterType));
            }

            foreach (var usado in usados.SelectMany(Expandir))
            {
                pares.Add((tipo.FullName, usado));
            }
        }

        return pares;
    }

    private static IEnumerable<TypeReference> Expandir(TypeReference tipo)
    {
        yield return tipo;

        if (tipo is TypeSpecification especificacao)
        {
            foreach (var interno in Expandir(especificacao.ElementType))
                yield return interno;
        }

        if (tipo is GenericInstanceType generico)
        {
            foreach (var argumento in generico.GenericArguments.SelectMany(Expandir))
                yield return argumento;
        }
    }
}
