using NetArchTest.Rules;

namespace Acordu.ArchitectureTests;

/// <summary>
/// ADR 0014, item 4, e F14-62 (RNF-PRIV-01): a área da Nebrys (casos de uso, DTOs e as consultas dela na Infrastructure)
/// não referencia tipos de negócio do prestador nem os DTOs deles, nem o portal. O que ela vê está declarado nos DTOs
/// próprios; com o contexto de tenant vazio, os filtros não devolveriam nada de qualquer jeito.
/// </summary>
public sealed class NebrysTests
{
    private const string AreaNaApplication = "Acordu.Application.Nebrys";
    private const string ConsultasDaArea = "Acordu.Infrastructure.Persistence.Nebrys";

    /// <summary>Clientes, contatos, solicitações, tarefas, horas, contratos, anexos e o que pende deles.</summary>
    private static readonly string[] TiposDeNegocio =
    [
        "Acordu.Domain.Entities.Cliente",
        "Acordu.Domain.Entities.Contato",
        "Acordu.Domain.Entities.Solicitacao",
        "Acordu.Domain.Entities.Interacao",
        "Acordu.Domain.Entities.Tarefa",
        "Acordu.Domain.Entities.ComentarioTarefa",
        "Acordu.Domain.Entities.ItemChecklist",
        "Acordu.Domain.Entities.RecorrenciaTarefa",
        "Acordu.Domain.Entities.Projeto",
        "Acordu.Domain.Entities.Apontamento",
        "Acordu.Domain.Entities.Cronometro",
        "Acordu.Domain.Entities.Contrato",
        "Acordu.Domain.Entities.AjusteSaldo",
        "Acordu.Domain.Entities.Anexo",
        "Acordu.Domain.Entities.AcessoPortal",
        "Acordu.Domain.Entities.ConvitePortal",
        "Acordu.Domain.Entities.Categoria",
        "Acordu.Application.Clientes",
        "Acordu.Application.Solicitacoes",
        "Acordu.Application.Tarefas",
        "Acordu.Application.Projetos",
        "Acordu.Application.Recorrencias",
        "Acordu.Application.Apontamentos",
        "Acordu.Application.Contratos",
        "Acordu.Application.Anexos",
        "Acordu.Application.Categorias",
        "Acordu.Application.Dashboard",
        "Acordu.Application.Agenda",
        "Acordu.Application.Atividades",
        "Acordu.Application.Colaboracao",
        "Acordu.Application.Capacidade",
        "Acordu.Application.AcessosPortal",
        "Acordu.Application.Portal",
    ];

    [Fact]
    public void AreaDaNebrys_ExisteNasDuasCamadas()
    {
        Camadas.Application.GetType(AreaNaApplication + ".IConsultasNebrys").Should().NotBeNull("o teste perde o sentido se o tipo mudar de nome");
        Camadas.Infrastructure.GetType(ConsultasDaArea + ".ConsultasNebrys").Should().NotBeNull("o teste perde o sentido se o tipo mudar de nome");
    }

    [Fact]
    public void ApplicationNebrys_NaoReferenciaTiposDeNegocioNemOPortal()
    {
        var resultado = Types.InAssembly(Camadas.Application)
            .That().ResideInNamespace(AreaNaApplication)
            .ShouldNot().HaveDependencyOnAny(TiposDeNegocio)
            .GetResult();

        resultado.IsSuccessful.Should().BeTrue(
            "a área da Nebrys só vê a relação com a empresa (RNF-PRIV-01); violações: {0}",
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }

    [Fact]
    public void ConsultasDaArea_NaoReferenciamTiposDeNegocio()
    {
        var resultado = Types.InAssembly(Camadas.Infrastructure)
            .That().ResideInNamespace(ConsultasDaArea)
            .ShouldNot().HaveDependencyOnAny(TiposDeNegocio)
            .GetResult();

        resultado.IsSuccessful.Should().BeTrue(
            "as consultas da área leem só o Tenant, a conta de quem criou e os vínculos; violações: {0}",
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }

    [Fact]
    public void DtosDaArea_SoComCamposDaRelacao()
    {
        var violacoes = Il.TiposReferenciados(Camadas.Application, AreaNaApplication)
            .Where(par => par.Referenciado.Namespace.StartsWith("Acordu.", StringComparison.Ordinal)
                && !par.Referenciado.Namespace.StartsWith(AreaNaApplication, StringComparison.Ordinal)
                && par.Referenciado.Name.EndsWith("Dto", StringComparison.Ordinal))
            .Select(par => $"{par.Origem} → {par.Referenciado.FullName}")
            .Distinct()
            .ToList();

        violacoes.Should().BeEmpty("a área da Nebrys tem DTOs próprios e nunca reaproveita DTO de outra área (ADR 0014, item 4)");
    }
}
