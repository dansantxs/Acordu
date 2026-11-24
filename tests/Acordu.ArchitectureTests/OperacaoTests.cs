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
            .And().DoNotHaveNameMatching("^ApagadorDeEmpresa")
            .ShouldNot().HaveDependencyOn(sugestao)
            .GetResult();

        fora.IsSuccessful.Should().BeTrue(
            "a Api só manda o texto e recebe o número; violações: {0}", string.Join(", ", fora.FailingTypeNames ?? []));
    }

    [Fact]
    public void Sugestoes_AApiSoUsaOsComandos() // F13-27
    {
        var naApi = Types.InAssembly(Camadas.Api)
            .ShouldNot().HaveDependencyOnAny("Acordu.Application.Sugestoes.ISugestoes", "Acordu.Application.Sugestoes.SugestaoParaAviso")
            .GetResult();

        naApi.IsSuccessful.Should().BeTrue(
            "a Api manda o texto e recebe o número, nunca lê sugestões; violações: {0}", string.Join(", ", naApi.FailingTypeNames ?? []));
    }

    [Fact] // Design do F14, D4 e D8 (F14g)
    public void DocumentoTeste_SoOCadastroEAAreaDaNebrysUsam()
    {
        const string documento = "Acordu.Domain.Entities.DocumentoTeste";
        Camadas.Domain.GetType(documento).Should().NotBeNull("o teste perde o sentido se o tipo mudar de nome");
        var fora = Types.InAssemblies([Camadas.Api, Camadas.Application, Camadas.Infrastructure])
            .That().DoNotResideInNamespace("Acordu.Infrastructure.Persistence.Documentos")
            .And().DoNotResideInNamespace("Acordu.Application.Nebrys")
            .And().DoNotResideInNamespace("Acordu.Infrastructure.Persistence.Migrations")
            .ShouldNot().HaveDependencyOn(documento)
            .GetResult();

        fora.IsSuccessful.Should().BeTrue(
            "a tabela global do documento é do registro do cadastro e da área da Nebrys (ADR 0014, item 7); violações: {0}",
            string.Join(", ", fora.FailingTypeNames ?? []));
    }

    [Fact] // Design do F14, D4 e D10 (F14h)
    public void AceiteTexto_SoORegistroDosAceitesUsa()
    {
        const string aceite = "Acordu.Domain.Entities.AceiteTexto";
        Camadas.Domain.GetType(aceite).Should().NotBeNull("o teste perde o sentido se o tipo mudar de nome");
        var fora = Types.InAssemblies([Camadas.Api, Camadas.Application, Camadas.Infrastructure])
            .That().DoNotResideInNamespace("Acordu.Infrastructure.Persistence.Aceites")
            .And().DoNotResideInNamespace("Acordu.Infrastructure.Persistence.Migrations")
            .ShouldNot().HaveDependencyOn(aceite)
            .GetResult();

        fora.IsSuccessful.Should().BeTrue(
            "a tabela global dos aceites é do registro dos aceites (ADR 0014, item 7); violações: {0}",
            string.Join(", ", fora.FailingTypeNames ?? []));
    }

    [Fact] // Design do F14, D10: o aceite é da conta, sem referência à empresa
    public void AceiteTexto_SemReferenciaAEmpresa()
    {
        var propriedades = Camadas.Domain.GetType("Acordu.Domain.Entities.AceiteTexto")!.GetProperties().Select(p => p.Name);

        propriedades.Should().BeEquivalentTo(["UsuarioId", "Texto", "Versao", "Forma", "RegistradoEm"]);
    }

    [Fact] // Design do F14, D4 e D12 (F14n)
    public void ConviteEmpresa_SoOCadastroEAAreaDaNebrysUsam()
    {
        const string convite = "Acordu.Domain.Entities.ConviteEmpresa";
        Camadas.Domain.GetType(convite).Should().NotBeNull("o teste perde o sentido se o tipo mudar de nome");
        var fora = Types.InAssemblies([Camadas.Api, Camadas.Application, Camadas.Infrastructure])
            .That().DoNotResideInNamespace("Acordu.Infrastructure.Persistence.ConvitesEmpresa")
            .And().DoNotResideInNamespace("Acordu.Application.Nebrys")
            .And().DoNotResideInNamespace("Acordu.Infrastructure.Persistence.Nebrys")
            .And().DoNotResideInNamespace("Acordu.Infrastructure.Persistence.Migrations")
            .ShouldNot().HaveDependencyOn(convite)
            .GetResult();

        fora.IsSuccessful.Should().BeTrue(
            "a tabela global dos convites de empresa é do registro dos convites e da área da Nebrys; o cadastro usa o registro "
            + "(ADR 0014, item 7); violações: {0}",
            string.Join(", ", fora.FailingTypeNames ?? []));
    }

    [Fact] // Design do F14, D12: o convite de empresa não tem referência a empresa (ela ainda não existe) nem a tenant
    public void ConviteEmpresa_SemReferenciaAEmpresa()
    {
        var propriedades = Camadas.Domain.GetType("Acordu.Domain.Entities.ConviteEmpresa")!.GetProperties().Select(p => p.Name);

        propriedades.Should().BeEquivalentTo(
            ["Id", "Email", "CriadoEm", "TokenHash", "EnviadoEm", "ExpiraEm", "UsadoEm", "RevogadoEm", "Pendente", "EntradaListaEsperaId"]);
    }

    [Fact] // Design do F14, D4 e D12 (F14o)
    public void EntradaListaEspera_SoAListaDeEsperaEAAreaDaNebrysUsam()
    {
        const string entrada = "Acordu.Domain.Entities.EntradaListaEspera";
        Camadas.Domain.GetType(entrada).Should().NotBeNull("o teste perde o sentido se o tipo mudar de nome");
        var fora = Types.InAssemblies([Camadas.Api, Camadas.Application, Camadas.Infrastructure])
            .That().DoNotResideInNamespace("Acordu.Application.ListaEspera")
            .And().DoNotResideInNamespace("Acordu.Infrastructure.Persistence.ListaEspera")
            .And().DoNotResideInNamespace("Acordu.Application.Nebrys")
            .And().DoNotResideInNamespace("Acordu.Infrastructure.Persistence.Nebrys")
            // Só a chave estrangeira da entrada de origem no mapeamento do convite (F14-76).
            .And().DoNotHaveNameMatching("^ConviteEmpresaConfiguration$")
            .And().DoNotResideInNamespace("Acordu.Infrastructure.Persistence.Migrations")
            .ShouldNot().HaveDependencyOn(entrada)
            .GetResult();

        fora.IsSuccessful.Should().BeTrue(
            "a tabela global da lista de espera é da inscrição pública e da área da Nebrys (ADR 0014, item 7); violações: {0}",
            string.Join(", ", fora.FailingTypeNames ?? []));
    }

    [Fact] // Design do F14, D12: a entrada da lista de espera não tem referência a empresa (ela ainda não existe) nem a tenant
    public void EntradaListaEspera_SemReferenciaAEmpresa()
    {
        var propriedades = Camadas.Domain.GetType("Acordu.Domain.Entities.EntradaListaEspera")!.GetProperties().Select(p => p.Name);

        propriedades.Should().BeEquivalentTo(["Id", "Nome", "Email", "NomeEmpresa", "Mensagem", "CriadaEm", "ConvidadaEm", "Situacao"]);
    }

    [Fact] // Design do F14, D8: o registro do documento não tem referência à empresa nem à pessoa
    public void DocumentoTeste_SemReferenciaAEmpresaNemAPessoa()
    {
        var propriedades = Camadas.Domain.GetType("Acordu.Domain.Entities.DocumentoTeste")!.GetProperties().Select(p => p.Name);

        propriedades.Should().BeEquivalentTo(["Hash", "PrimeiroTesteEm", "LiberadoEm", "LiberacaoUsadaEm", "LiberacaoDisponivel"]);
    }
}
