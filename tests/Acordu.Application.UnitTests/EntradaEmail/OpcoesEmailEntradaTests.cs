using Acordu.Application.EntradaEmail;
using Acordu.Domain.Entities;

namespace Acordu.Application.UnitTests.EntradaEmail;

/// <summary>
/// Design do F15, D2 e §1.2 (F15-94): o domínio de entrada vem de <c>EmailEntrada__Dominio</c>; sem ele não há endereço,
/// e valor inválido impede a subida.
/// </summary>
public sealed class OpcoesEmailEntradaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 6, 13, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DaConfiguracao_SemDominio_Indisponivel(string? dominio)
    {
        var opcoes = OpcoesEmailEntrada.DaConfiguracao(dominio);

        opcoes.Disponivel.Should().BeFalse();
        opcoes.Dominio.Should().BeNull();
        opcoes.EnderecoDe(Empresa()).Should().BeNull("sem o domínio, nenhuma empresa tem endereço (F15-94)");
    }

    [Theory]
    [InlineData("entrada.acordu.app.br", "entrada.acordu.app.br")]
    [InlineData(" Entrada.Acordu.App.BR ", "entrada.acordu.app.br")]
    [InlineData("in-box.exemplo.com", "in-box.exemplo.com")]
    public void DaConfiguracao_DominioValido_NormalizadoEmMinusculas(string dominio, string esperado)
    {
        OpcoesEmailEntrada.DaConfiguracao(dominio).Dominio.Should().Be(esperado);
    }

    [Theory]
    [InlineData("entrada")]
    [InlineData("entrada.acordu.app.br/")]
    [InlineData("@entrada.acordu.app.br")]
    [InlineData("https://entrada.acordu.app.br")]
    [InlineData("-entrada.acordu.app.br")]
    [InlineData("entrada..acordu.app.br")]
    [InlineData("entrada acordu.app.br")]
    public void DaConfiguracao_DominioInvalido_ImpedeASubida(string dominio)
    {
        var acao = () => OpcoesEmailEntrada.DaConfiguracao(dominio);

        acao.Should().Throw<InvalidOperationException>().WithMessage("*EmailEntrada__Dominio*");
    }

    [Fact]
    public void EnderecoDe_EmpresaComCodigo_CodigoArrobaDominio() // F15-95
    {
        var empresa = Empresa();

        var endereco = OpcoesEmailEntrada.DaConfiguracao("entrada.acordu.app.br").EnderecoDe(empresa);

        endereco.Should().Be($"{empresa.CodigoEntrada}@entrada.acordu.app.br");
    }

    [Fact]
    public void EnderecoDe_Demonstracao_Nulo() // F15-96
    {
        var demonstracao = Tenant.CriarDemonstracao(Guid.NewGuid(), "Estúdio Aurora", Agora, Guid.NewGuid());

        OpcoesEmailEntrada.DaConfiguracao("entrada.acordu.app.br").EnderecoDe(demonstracao).Should().BeNull();
    }

    [Fact]
    public void Construtor_DominioInvalido_ArgumentException()
    {
        var acao = () => new OpcoesEmailEntrada("sem ponto");

        acao.Should().Throw<ArgumentException>().WithParameterName("dominio");
    }

    private static Tenant Empresa() => Tenant.Criar(Guid.NewGuid(), "Estúdio Traço", Agora, Guid.NewGuid(), TimeSpan.FromDays(30));
}
