using NetArchTest.Rules;

namespace Acordu.ArchitectureTests;

/// <summary>Spec do F13, F13-12: as ocorrências de erro são da operação, fora dos tenants, e nenhuma rota as lê.</summary>
public sealed class OperacaoTests
{
    private const string Ocorrencia = "Acordu.Infrastructure.Operacao.OcorrenciaErro";

    [Fact]
    public void OcorrenciaErro_SoAOperacaoDaInfrastructureUsa()
    {
        Camadas.Infrastructure.GetType(Ocorrencia).Should().NotBeNull("o teste perde o sentido se o tipo mudar de nome");
        var foraDaOperacao = Types.InAssemblies([Camadas.Api, Camadas.Application, Camadas.Infrastructure])
            .That().DoNotResideInNamespace("Acordu.Infrastructure.Operacao")
            .And().DoNotResideInNamespace("Acordu.Infrastructure.Persistence.Migrations")
            .ShouldNot().HaveDependencyOn(Ocorrencia)
            .GetResult();

        foraDaOperacao.IsSuccessful.Should().BeTrue(
            "só a deduplicação e o expurgo tocam as ocorrências; violações: {0}",
            string.Join(", ", foraDaOperacao.FailingTypeNames ?? []));
    }
}
