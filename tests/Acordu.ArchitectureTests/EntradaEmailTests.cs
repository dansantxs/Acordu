using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Acordu.ArchitectureTests;

/// <summary>
/// Design do F15, §1.2 e D14 (ADR 0016, item 3; convenções §1): a consulta global do código de entrada (destino → empresa)
/// fica num ponto revisado só, em <c>Persistence.EntradaEmail</c>. Um método que ignora os filtros e lê o código da
/// empresa em outro lugar abriria um segundo caminho de destino → tenant, fora da revisão.
/// </summary>
public sealed class EntradaEmailTests
{
    private const string PontoRevisado = "Acordu.Infrastructure.Persistence.EntradaEmail.";

    [Fact]
    public void ConsultaGlobalDoCodigo_SoEmPersistenceEntradaEmail()
    {
        var consultas = MetodosQueIgnoramFiltrosELeemOCodigo();

        consultas.Should().NotBeEmpty("o teste perde o sentido se a consulta revisada mudar de forma");
        consultas.Should().OnlyContain(
            metodo => metodo.StartsWith(PontoRevisado, StringComparison.Ordinal),
            "destino → empresa é só a consulta revisada (EmpresasDeEntrada)");
    }

    /// <summary>
    /// Métodos da Infrastructure (fora das migrations) que chamam <c>IgnoreQueryFilters</c> e citam o getter de
    /// <c>Tenant.CodigoEntrada</c> numa árvore de expressão (<c>ldtoken</c>), inclusive os da máquina de estados de um
    /// método assíncrono (tipo aninhado, sem namespace próprio: vale o nome completo).
    /// </summary>
    private static List<string> MetodosQueIgnoramFiltrosELeemOCodigo()
    {
        using var modulo = ModuleDefinition.ReadModule(Camadas.Infrastructure.Location);
        var encontrados = new List<string>();
        foreach (var tipo in modulo.GetTypes().Where(t => !t.FullName.StartsWith("Acordu.Infrastructure.Persistence.Migrations", StringComparison.Ordinal)))
        {
            foreach (var metodo in tipo.Methods.Where(m => m.HasBody))
            {
                var instrucoes = metodo.Body.Instructions;
                var ignora = instrucoes.Any(i => i.Operand is MethodReference { Name: "IgnoreQueryFilters" });
                var codigo = instrucoes.Any(i =>
                    i.OpCode.Code == Code.Ldtoken
                    && i.Operand is MethodReference { Name: "get_CodigoEntrada" } getter
                    && getter.DeclaringType.FullName == "Acordu.Domain.Entities.Tenant");
                if (ignora && codigo)
                    encontrados.Add($"{tipo.FullName}.{metodo.Name}");
            }
        }

        return encontrados;
    }
}
