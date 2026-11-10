using NetArchTest.Rules;

namespace Acordu.ArchitectureTests;

/// <summary>Spec do F13, F13-12: as ocorrências de erro são da operação, fora dos tenants, e nenhuma rota as lê.</summary>
public sealed class OperacaoTests
{
    private const string Ocorrencia = "Acordu.Infrastructure.Operacao.OcorrenciaErro";
    private const string Marca = "Acordu.Infrastructure.Operacao.MarcaOperacao";

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

    [Fact]
    public void MarcaOperacao_SoAOperacaoDaInfrastructureUsa()
    {
        Camadas.Infrastructure.GetType(Marca).Should().NotBeNull("o teste perde o sentido se o tipo mudar de nome");
        var foraDaOperacao = Types.InAssemblies([Camadas.Api, Camadas.Application, Camadas.Infrastructure])
            .That().DoNotResideInNamespace("Acordu.Infrastructure.Operacao")
            .And().DoNotResideInNamespace("Acordu.Infrastructure.Persistence.Migrations")
            .ShouldNot().HaveDependencyOn(Marca)
            .GetResult();

        foraDaOperacao.IsSuccessful.Should().BeTrue(
            "só as marcas e o expurgo tocam a tabela; violações: {0}", string.Join(", ", foraDaOperacao.FailingTypeNames ?? []));
    }

    [Fact]
    public void Sugestao_NenhumaRotaNemOutraFeatureUsa() // F13-27
    {
        const string sugestao = "Acordu.Domain.Entities.Sugestao";
        Camadas.Domain.GetType(sugestao).Should().NotBeNull("o teste perde o sentido se o tipo mudar de nome");
        var fora = Types.InAssemblies([Camadas.Api, Camadas.Application, Camadas.Infrastructure])
            .That().DoNotResideInNamespace("Acordu.Application.Sugestoes")
            .And().DoNotResideInNamespace("Acordu.Infrastructure.Persistence.Sugestoes")
            .And().DoNotResideInNamespace("Acordu.Infrastructure.Persistence.Migrations")
            .And().DoNotHaveNameMatching("^ExpurgoCadastros")
            .ShouldNot().HaveDependencyOn(sugestao)
            .GetResult();

        fora.IsSuccessful.Should().BeTrue(
            "a Api só manda o texto e recebe o número; violações: {0}", string.Join(", ", fora.FailingTypeNames ?? []));
    }
}
