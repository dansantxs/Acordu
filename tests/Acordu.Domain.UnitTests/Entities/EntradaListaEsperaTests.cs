using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;

namespace Acordu.Domain.UnitTests.Entities;

/// <summary>Spec do F14, F14-52, F14-55 e F14-76 (F14o): a entrada da lista de espera (design D12).</summary>
public sealed class EntradaListaEsperaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact] // F14-52
    public void Registrar_CamposValidos_AguardandoComEmailNormalizadoEADataEmUtc()
    {
        var id = Guid.NewGuid();

        var entrada = EntradaListaEspera.Registrar(
            id, "  Ana Souza ", " Ana@Estudio.COM ", " Estúdio Traço ", "  Controlar as horas dos clientes. ", Agora.ToOffset(TimeSpan.FromHours(-3)));

        entrada.Id.Should().Be(id);
        entrada.Nome.Should().Be("Ana Souza");
        entrada.Email.Should().Be("ana@estudio.com");
        entrada.NomeEmpresa.Should().Be("Estúdio Traço");
        entrada.Mensagem.Should().Be("Controlar as horas dos clientes.");
        entrada.CriadaEm.Should().Be(Agora);
        entrada.CriadaEm.Offset.Should().Be(TimeSpan.Zero);
        entrada.ConvidadaEm.Should().BeNull();
        entrada.Situacao.Should().Be(SituacaoEntradaListaEspera.Aguardando);
    }

    [Theory] // F14-52: a mensagem é opcional
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Registrar_SemMensagem_MensagemNula(string? mensagem)
    {
        var entrada = EntradaListaEspera.Registrar(Guid.NewGuid(), "Ana", "ana@exemplo.com", "Estúdio", mensagem, Agora);

        entrada.Mensagem.Should().BeNull();
    }

    [Theory] // F14-55
    [InlineData(null, "ana@exemplo.com", "Estúdio", "nome")]
    [InlineData("  ", "ana@exemplo.com", "Estúdio", "nome")]
    [InlineData("Ana", null, "Estúdio", "email")]
    [InlineData("Ana", "", "Estúdio", "email")]
    [InlineData("Ana", "sem-arroba", "Estúdio", "email")]
    [InlineData("Ana", "sérgio@exemplo.com", "Estúdio", "email")]
    [InlineData("Ana", "ana@exemplo.com", null, "nomeEmpresa")]
    [InlineData("Ana", "ana@exemplo.com", " ", "nomeEmpresa")]
    public void Registrar_ObrigatorioFaltandoOuEmailInvalido_LancaNoCampo(string? nome, string? email, string? nomeEmpresa, string campo)
    {
        var acao = () => EntradaListaEspera.Registrar(Guid.NewGuid(), nome, email, nomeEmpresa, null, Agora);

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be(campo);
    }

    [Fact] // F14-55: o primeiro campo do formulário que falta é o que volta
    public void Registrar_VariosCamposFaltando_LancaNoPrimeiro()
    {
        var acao = () => EntradaListaEspera.Registrar(Guid.NewGuid(), null, null, null, null, Agora);

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("nome");
    }

    [Fact] // F14-55
    public void Registrar_TextosLongosDemais_LancaNoCampo()
    {
        var longo = new string('a', LimitesTexto.Nome + 1);

        ((Action)(() => EntradaListaEspera.Registrar(Guid.NewGuid(), longo, "ana@exemplo.com", "Estúdio", null, Agora)))
            .Should().Throw<ArgumentException>().Which.ParamName.Should().Be("nome");
        ((Action)(() => EntradaListaEspera.Registrar(Guid.NewGuid(), "Ana", "ana@exemplo.com", longo, null, Agora)))
            .Should().Throw<ArgumentException>().Which.ParamName.Should().Be("nomeEmpresa");
        ((Action)(() => EntradaListaEspera.Registrar(
                Guid.NewGuid(), "Ana", "ana@exemplo.com", "Estúdio", new string('a', LimitesTexto.MensagemListaEspera + 1), Agora)))
            .Should().Throw<ArgumentException>().Which.ParamName.Should().Be("mensagem");
    }

    [Fact]
    public void Registrar_IdVazio_Lanca()
    {
        var acao = () => EntradaListaEspera.Registrar(Guid.Empty, "Ana", "ana@exemplo.com", "Estúdio", null, Agora);

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("id");
    }

    [Fact] // F14-76
    public void Convidar_Aguardando_FicaConvidadaComAData()
    {
        var entrada = EntradaListaEspera.Registrar(Guid.NewGuid(), "Ana", "ana@exemplo.com", "Estúdio", null, Agora);

        entrada.Convidar(Agora.AddDays(2));

        entrada.ConvidadaEm.Should().Be(Agora.AddDays(2));
        entrada.Situacao.Should().Be(SituacaoEntradaListaEspera.Convidada);
    }

    [Fact] // F14-76: só a entrada aguardando vira convite
    public void Convidar_JaConvidada_LancaENaoMudaAData()
    {
        var entrada = EntradaListaEspera.Registrar(Guid.NewGuid(), "Ana", "ana@exemplo.com", "Estúdio", null, Agora);
        entrada.Convidar(Agora.AddDays(1));

        var acao = () => entrada.Convidar(Agora.AddDays(2));

        acao.Should().Throw<TransicaoInvalidaException>();
        entrada.ConvidadaEm.Should().Be(Agora.AddDays(1));
    }
}
