using Acordu.Application.EntradaEmail;

namespace Acordu.Application.UnitTests.EntradaEmail;

/// <summary>
/// Spec do F15, termo "remetente conhecido" (F15-10, F15-27; design §1.2 e D11): um endereço só no <c>From</c>, normalizado
/// como o dos contatos, de exatamente um contato ativo de cliente ativo, com a autenticidade confirmada.
/// </summary>
public sealed class IdentificacaoDoRemetenteTests
{
    private static ContatoDeEntrada Contato(bool clienteAtivo = true, bool contatoAtivo = true) =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, clienteAtivo, contatoAtivo);

    [Theory]
    [InlineData("Ana@Cliente.COM.br", "ana@cliente.com.br")]
    [InlineData(" ana@cliente.com.br ", "ana@cliente.com.br")]
    public void EmailDoRemetente_UmEndereco_NormalizadoComoOsContatos(string remetente, string esperado)
    {
        IdentificacaoDoRemetente.EmailDoRemetente([remetente]).Should().Be(esperado);
    }

    [Fact]
    public void EmailDoRemetente_NenhumOuVarios_Nulo()
    {
        IdentificacaoDoRemetente.EmailDoRemetente([]).Should().BeNull();
        IdentificacaoDoRemetente.EmailDoRemetente(["ana@cliente.com.br", "beto@cliente.com.br"]).Should().BeNull();
    }

    [Fact]
    public void EmailDoRemetente_Invalido_Nulo()
    {
        IdentificacaoDoRemetente.EmailDoRemetente(["sem-arroba"]).Should().BeNull();
    }

    [Theory] // D11: só pass confirma
    [InlineData("pass", true)]
    [InlineData(" PASS ", true)]
    [InlineData("fail", false)]
    [InlineData("none", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void AutenticidadeOk_Veredito_SoPassConfirma(string? veredito, bool esperado)
    {
        IdentificacaoDoRemetente.AutenticidadeOk(veredito).Should().Be(esperado);
    }

    [Fact] // F15-10
    public void Escolher_UmAtivoDeClienteAtivo_Ele()
    {
        var contato = Contato();

        IdentificacaoDoRemetente.Escolher([contato, Contato(contatoAtivo: false)]).Should().Be(contato);
    }

    [Fact] // F15-24 (a triagem é do F15h)
    public void Escolher_AtivosEmDoisClientes_Nulo()
    {
        IdentificacaoDoRemetente.Escolher([Contato(), Contato()]).Should().BeNull();
    }

    [Fact] // F15-25 (a triagem é do F15h)
    public void Escolher_SoInativosOuDeClienteInativo_Nulo()
    {
        IdentificacaoDoRemetente.Escolher([Contato(contatoAtivo: false), Contato(clienteAtivo: false)]).Should().BeNull();
        IdentificacaoDoRemetente.Escolher([]).Should().BeNull();
    }
}
