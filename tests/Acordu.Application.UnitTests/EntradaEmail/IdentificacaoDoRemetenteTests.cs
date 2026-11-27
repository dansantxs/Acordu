using Acordu.Application.EntradaEmail;
using Acordu.Domain.Enums;

namespace Acordu.Application.UnitTests.EntradaEmail;

/// <summary>
/// Spec do F15, termo "remetente conhecido" (F15-10, F15-27; design §1.2 e D11): um endereço só no <c>From</c>, normalizado
/// como o dos contatos, de exatamente um contato ativo de cliente ativo, com a autenticidade confirmada. F15h (F15-20,
/// F15-24 a F15-26; design D10): o motivo da triagem de quem não é conhecido.
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

    [Fact] // F15-24
    public void Escolher_AtivosEmDoisClientes_Nulo()
    {
        IdentificacaoDoRemetente.Escolher([Contato(), Contato()]).Should().BeNull();
    }

    [Fact] // F15-25
    public void Escolher_SoInativosOuDeClienteInativo_Nulo()
    {
        IdentificacaoDoRemetente.Escolher([Contato(contatoAtivo: false), Contato(clienteAtivo: false)]).Should().BeNull();
        IdentificacaoDoRemetente.Escolher([]).Should().BeNull();
    }

    [Fact] // F15-10
    public void Classificar_UmAtivoDeClienteAtivoComAutenticidade_OContatoSemMotivo()
    {
        var contato = Contato();

        var identificacao = IdentificacaoDoRemetente.Classificar(true, [contato, Contato(contatoAtivo: false)]);

        identificacao.Should().BeEquivalentTo(new Identificacao(contato, null, []));
    }

    [Theory] // F15-20: ninguém com o e-mail é desconhecido, com ou sem a autenticidade
    [InlineData(true)]
    [InlineData(false)]
    public void Classificar_NenhumContato_Desconhecido(bool autenticidadeOk)
    {
        var identificacao = IdentificacaoDoRemetente.Classificar(autenticidadeOk, []);

        identificacao.Contato.Should().BeNull();
        identificacao.Motivo.Should().Be(MotivoTriagem.Desconhecido);
        identificacao.ClientesPossiveis.Should().BeEmpty();
    }

    [Fact] // F15-26: o remetente declarado é um contato, mas o domínio não confirmou
    public void Classificar_ContatoSemAutenticidade_RemetenteNaoConfirmado()
    {
        var identificacao = IdentificacaoDoRemetente.Classificar(false, [Contato()]);

        identificacao.Contato.Should().BeNull();
        identificacao.Motivo.Should().Be(MotivoTriagem.RemetenteNaoConfirmado);
    }

    [Fact] // F15-26 vem antes do F15-24 e do F15-25: sem a autenticidade, o resto do cadastro não vale
    public void Classificar_DoisClientesOuInativoSemAutenticidade_RemetenteNaoConfirmado()
    {
        IdentificacaoDoRemetente.Classificar(false, [Contato(), Contato()]).Motivo.Should().Be(MotivoTriagem.RemetenteNaoConfirmado);
        IdentificacaoDoRemetente.Classificar(false, [Contato(contatoAtivo: false)]).Motivo.Should().Be(MotivoTriagem.RemetenteNaoConfirmado);
    }

    [Fact] // F15-24: os clientes possíveis são os dos contatos ativos de clientes ativos
    public void Classificar_AtivosEmDoisClientes_MaisDeUmClienteComOsClientes()
    {
        var a = Contato();
        var b = Contato();

        var identificacao = IdentificacaoDoRemetente.Classificar(true, [a, b, Contato(clienteAtivo: false)]);

        identificacao.Contato.Should().BeNull();
        identificacao.Motivo.Should().Be(MotivoTriagem.MaisDeUmCliente);
        identificacao.ClientesPossiveis.Should().Equal(a.ClienteId, b.ClienteId);
    }

    [Fact] // F15-25: contato inativo, ou contato de cliente inativo
    public void Classificar_SoInativosOuDeClienteInativo_ContatoInativo()
    {
        var identificacao = IdentificacaoDoRemetente.Classificar(true, [Contato(contatoAtivo: false), Contato(clienteAtivo: false)]);

        identificacao.Contato.Should().BeNull();
        identificacao.Motivo.Should().Be(MotivoTriagem.ContatoInativo);
        identificacao.ClientesPossiveis.Should().BeEmpty();
    }

    [Fact] // RF-CLI-02 impede dois ativos no mesmo cliente; se aparecerem, ninguém é escolhido sozinho
    public void Classificar_DoisAtivosDoMesmoCliente_DesconhecidoSemEscolher()
    {
        var cliente = Guid.NewGuid();

        var identificacao = IdentificacaoDoRemetente.Classificar(
            true, [new ContatoDeEntrada(cliente, Guid.NewGuid(), null, true, true), new ContatoDeEntrada(cliente, Guid.NewGuid(), null, true, true)]);

        identificacao.Contato.Should().BeNull();
        identificacao.Motivo.Should().Be(MotivoTriagem.Desconhecido);
    }

    [Fact] // F15-24: o cliente inativo não conta como possível; sobrando um ativo só, ele é o remetente conhecido
    public void Classificar_AtivoEmUmClienteEOutroDeClienteInativo_OContatoDoAtivo()
    {
        var ativo = Contato();

        var identificacao = IdentificacaoDoRemetente.Classificar(true, [Contato(clienteAtivo: false), ativo]);

        identificacao.Contato.Should().Be(ativo);
        identificacao.Motivo.Should().BeNull();
        identificacao.ClientesPossiveis.Should().BeEmpty();
    }

    [Fact] // F15-24: mais de dois clientes, todos listados, sem repetir e na ordem em que vieram
    public void Classificar_AtivosEmTresClientes_TodosOsClientesPossiveis()
    {
        var a = Contato();
        var b = Contato();
        var c = Contato();

        var identificacao = IdentificacaoDoRemetente.Classificar(true, [a, b, c, Contato(contatoAtivo: false)]);

        identificacao.Motivo.Should().Be(MotivoTriagem.MaisDeUmCliente);
        identificacao.ClientesPossiveis.Should().Equal(a.ClienteId, b.ClienteId, c.ClienteId);
    }

    [Theory] // F15-20: um endereço só, mas inválido (vazio, sem domínio, acento antes do @), não é remetente
    [InlineData("")]
    [InlineData("@sem-local.com.br")]
    [InlineData("ana@semponto")]
    [InlineData("sérgio@x.com.br")]
    public void EmailDoRemetente_EnderecoUnicoInvalido_Nulo(string remetente)
    {
        IdentificacaoDoRemetente.EmailDoRemetente([remetente]).Should().BeNull();
    }

    [Fact] // F15-30 (F15i): quem responde à conversa é o contato da solicitação, ativo, de cliente ativo, autenticado
    public void DaSolicitacao_OContatoDaSolicitacaoAutenticado_OContato()
    {
        var contato = Contato();
        var deOutroCliente = new ContatoDeEntrada(Guid.NewGuid(), Guid.NewGuid(), null, true, true);

        IdentificacaoDoRemetente.DaSolicitacao(true, [deOutroCliente, contato], contato.ClienteId, contato.ContatoId)
            .Should().Be(contato);
    }

    [Fact] // até o F15j, o colega do mesmo cliente não é quem responde (F15-38 é do F15j)
    public void DaSolicitacao_ColegaDoMesmoCliente_Nulo()
    {
        var contato = Contato();
        var colega = contato with { ContatoId = Guid.NewGuid() };

        IdentificacaoDoRemetente.DaSolicitacao(true, [colega], contato.ClienteId, contato.ContatoId).Should().BeNull();
    }

    [Theory] // D11 e RF-CLI-02: sem autenticidade, ou com o contato ou o cliente inativos, ninguém responde
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void DaSolicitacao_SemAutenticidadeOuInativo_Nulo(bool autenticidadeOk, bool clienteAtivo, bool contatoAtivo)
    {
        var contato = Contato(clienteAtivo, contatoAtivo);

        IdentificacaoDoRemetente.DaSolicitacao(autenticidadeOk, [contato], contato.ClienteId, contato.ContatoId).Should().BeNull();
    }

    [Fact] // solicitação sem contato: não há de quem seja a resposta
    public void DaSolicitacao_SolicitacaoSemContato_Nulo()
    {
        var contato = Contato();

        IdentificacaoDoRemetente.DaSolicitacao(true, [contato], contato.ClienteId, null).Should().BeNull();
    }
}
