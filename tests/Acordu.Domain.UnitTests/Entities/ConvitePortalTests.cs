using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;
using Acordu.Domain.Exceptions;
using static Acordu.Domain.UnitTests.Calendarios;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class ConvitePortalTests
{
    private static readonly Guid Contato = Guid.NewGuid();

    private static ConvitePortal Criar(EscopoPortal escopo = EscopoPortal.TodasDoCliente) =>
        ConvitePortal.Criar(Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, Contato, escopo, Brasilia(6, 10));

    [Fact]
    public void Criar_PendenteSemToken_EmiteEvento()
    {
        var convite = Criar();

        convite.Pendente.Should().BeTrue();
        convite.TokenHash.Should().BeNull();
        convite.ExpiraEm.Should().BeNull();
        convite.Escopo.Should().Be(EscopoPortal.TodasDoCliente);
        convite.Eventos.Should().HaveCount(1);
        var evento = (ConvitePortalCriado)convite.Eventos[0];
        evento.ConviteId.Should().Be(convite.Id);
        evento.ContatoId.Should().Be(Contato);
        evento.ClienteId.Should().Be(Contratos.Cliente);
    }

    [Fact]
    public void Criar_EscopoInvalido_LancaExcecao()
    {
        var acao = () => Criar((EscopoPortal)99);

        acao.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GerarToken_GuardaSoOHash_EValidadeDeSeteDias()
    {
        var convite = Criar();

        var token = convite.GerarToken(Brasilia(6, 11));

        token.Should().HaveLength(43);
        convite.TokenHash.Should().Be(AcessoPortal.HashDoToken(token));
        convite.ExpiraEm.Should().Be(Brasilia(13, 11));
    }

    [Fact]
    public void Validar_SoComOTokenDoUltimoEnvio_EDentroDaValidade()
    {
        var convite = Criar();
        var primeiro = convite.GerarToken(Brasilia(6, 11));
        var segundo = convite.GerarToken(Brasilia(7, 11));

        convite.Validar(primeiro, Brasilia(7, 12)).Should().BeFalse();
        convite.Validar(segundo, Brasilia(7, 12)).Should().BeTrue();
        convite.Validar(segundo, Brasilia(14, 11)).Should().BeFalse();
        convite.Validar(null, Brasilia(7, 12)).Should().BeFalse();
    }

    [Fact]
    public void Validar_AntesDoEnvio_Falso()
    {
        Criar().Validar("qualquer", Brasilia(6, 11)).Should().BeFalse();
    }

    [Fact]
    public void Aceitar_TokenValido_GuardaUsuario_EDeixaDeSerPendente()
    {
        var convite = Criar();
        var token = convite.GerarToken(Brasilia(6, 11));
        var usuario = Guid.NewGuid();

        convite.Aceitar(token, usuario, Brasilia(7, 9));

        convite.Pendente.Should().BeFalse();
        convite.UsuarioId.Should().Be(usuario);
        convite.AceitoEm.Should().Be(Brasilia(7, 9));
        convite.Validar(token, Brasilia(7, 10)).Should().BeFalse();
    }

    [Fact]
    public void Aceitar_TokenErradoOuExpirado_LancaExcecao()
    {
        var convite = Criar();
        var token = convite.GerarToken(Brasilia(6, 11));

        var errado = () => convite.Aceitar("outro", Guid.NewGuid(), Brasilia(7, 9));
        var expirado = () => convite.Aceitar(token, Guid.NewGuid(), Brasilia(13, 11));

        errado.Should().Throw<AcessoPortalException>();
        expirado.Should().Throw<AcessoPortalException>();
    }

    [Fact]
    public void Revogar_Pendente_InvalidaOToken()
    {
        var convite = Criar();
        var token = convite.GerarToken(Brasilia(6, 11));

        convite.Revogar(Brasilia(6, 12));

        convite.Pendente.Should().BeFalse();
        convite.RevogadoEm.Should().Be(Brasilia(6, 12));
        convite.Validar(token, Brasilia(6, 13)).Should().BeFalse();
        var gerar = () => convite.GerarToken(Brasilia(6, 13));
        gerar.Should().Throw<AcessoPortalException>();
    }

    [Fact]
    public void Revogar_Aceito_NaoMuda()
    {
        var convite = Criar();
        convite.Aceitar(convite.GerarToken(Brasilia(6, 11)), Guid.NewGuid(), Brasilia(6, 12));

        convite.Revogar(Brasilia(6, 13));

        convite.RevogadoEm.Should().BeNull();
        convite.AceitoEm.Should().NotBeNull();
    }

    [Fact]
    public void AlterarEscopo_Pendente_Altera_EAceitoLanca()
    {
        var convite = Criar();

        convite.AlterarEscopo(EscopoPortal.Proprias);
        convite.Escopo.Should().Be(EscopoPortal.Proprias);

        convite.Aceitar(convite.GerarToken(Brasilia(6, 11)), Guid.NewGuid(), Brasilia(6, 12));
        var acao = () => convite.AlterarEscopo(EscopoPortal.TodasDoCliente);
        acao.Should().Throw<AcessoPortalException>();
    }
}
