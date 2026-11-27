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
    /// F15e (design D16; ADR 0015, item 3): o registro de entrada sem os filtros (o expurgo de todos os tenants, inclusive
    /// os só leitura) só no ponto revisado. Outro <c>IgnoreQueryFilters</c> sobre <c>EmailRecebido</c> seria um segundo
    /// caminho fora das guardas.
    /// </summary>
    [Fact]
    public void RegistroDeEntradaSemFiltros_SoEmPersistenceEntradaEmail()
    {
        var metodos = MetodosQueIgnoramFiltrosDe("Acordu.Domain.Entities.EmailRecebido");

        metodos.Should().NotBeEmpty("o expurgo do registro de entrada é o ponto revisado que este teste guarda");
        metodos.Should().OnlyContain(
            metodo => metodo.StartsWith(PontoRevisado, StringComparison.Ordinal),
            "o registro de entrada sem filtros é só o expurgo revisado (ExpurgoEmailEntrada)");
    }

    /// <summary>
    /// F15f (design §1.2; ADR 0016, item 3): os contatos do remetente são procurados só na empresa do destino, sob os
    /// filtros. <c>IgnoreQueryFilters</c> em <c>ContatosDeEntrada</c> (inclusive na máquina de estados do método assíncrono,
    /// um tipo aninhado) deixaria um contato de outra empresa abrir solicitação nesta.
    /// </summary>
    [Fact]
    public void ContatosDoRemetente_SemIgnoreQueryFilters()
    {
        const string Consulta = PontoRevisado + "ContatosDeEntrada";
        using var modulo = ModuleDefinition.ReadModule(Camadas.Infrastructure.Location);
        var tipos = modulo.GetTypes()
            .Where(t => t.FullName == Consulta || t.FullName.StartsWith(Consulta + "/", StringComparison.Ordinal))
            .ToList();

        tipos.Should().Contain(t => t.FullName == Consulta, "o teste perde o sentido se a consulta mudar de nome");
        tipos.SelectMany(t => t.Methods.Where(m => m.HasBody).Select(m => (Tipo: t, Metodo: m)))
            .Where(par => par.Metodo.Body.Instructions.Any(i => i.Operand is MethodReference { Name: "IgnoreQueryFilters" }))
            .Select(par => $"{par.Tipo.FullName}.{par.Metodo.Name}")
            .Should().BeEmpty("a identificação do remetente roda sob os filtros do tenant do destino");
    }

    /// <summary>Métodos da Infrastructure (fora das migrations) que chamam <c>IgnoreQueryFilters&lt;T&gt;</c> com o tipo dado.</summary>
    private static List<string> MetodosQueIgnoramFiltrosDe(string entidade)
    {
        using var modulo = ModuleDefinition.ReadModule(Camadas.Infrastructure.Location);
        var encontrados = new List<string>();
        foreach (var tipo in modulo.GetTypes().Where(t => !t.FullName.StartsWith("Acordu.Infrastructure.Persistence.Migrations", StringComparison.Ordinal)))
        {
            foreach (var metodo in tipo.Methods.Where(m => m.HasBody))
            {
                if (metodo.Body.Instructions.Any(i => i.Operand is GenericInstanceMethod { Name: "IgnoreQueryFilters" } chamada
                    && chamada.GenericArguments.Any(a => a.FullName == entidade)))
                    encontrados.Add($"{tipo.FullName}.{metodo.Name}");
            }
        }

        return encontrados;
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
