using Acordu.Domain.Entities;
using Acordu.Domain.Events;
using Acordu.Domain.Exceptions;
using static Acordu.Domain.UnitTests.Calendarios;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class ConviteEquipeTests
{
    private static readonly IReadOnlyList<Papel> Padroes = Papel.Padroes(Calendarios.Tenant);

    private static Papel Membro => Padroes.Single(p => p.Nome == Papel.NomeMembro);

    private static ConviteEquipe Criar(string email = " Ana@Estudio.COM ") =>
        ConviteEquipe.Criar(Guid.NewGuid(), email, Membro, Guid.NewGuid(), Brasilia(6, 10));

    [Fact]
    public void Criar_NormalizaOEmail_GuardaOPapel_EEmiteEvento()
    {
        var convite = Criar();

        convite.Email.Should().Be("ana@estudio.com");
        convite.PapelId.Should().Be(Membro.Id);
        convite.TenantId.Should().Be(Calendarios.Tenant);
        convite.Pendente.Should().BeTrue();
        convite.TokenHash.Should().BeNull();
        convite.Eventos.Should().HaveCount(1);
        ((ConviteEquipeCriado)convite.Eventos[0]).ConviteId.Should().Be(convite.Id);
    }

    [Fact]
    public void Criar_PapelCliente_LancaExcecao()
    {
        var cliente = Padroes.Single(p => p.Nome == Papel.NomeCliente);

        var acao = () => ConviteEquipe.Criar(Guid.NewGuid(), "ana@estudio.com", cliente, Guid.NewGuid(), Brasilia(6, 10));

        acao.Should().Throw<EquipeException>();
    }

    [Fact]
    public void Criar_EmailInvalido_LancaExcecaoNoCampo()
    {
        var acao = () => Criar("ana-sem-arroba");

        acao.Should().Throw<ArgumentException>().WithParameterName("email");
    }

    [Fact]
    public void GerarToken_SoOUltimoValeDentroDosSeteDias()
    {
        var convite = Criar();
        var primeiro = convite.GerarToken(Brasilia(6, 11));
        var segundo = convite.GerarToken(Brasilia(7, 11));

        convite.ExpiraEm.Should().Be(Brasilia(14, 11));
        convite.Validar(primeiro, Brasilia(7, 12)).Should().BeFalse();
        convite.Validar(segundo, Brasilia(7, 12)).Should().BeTrue();
        convite.Validar(segundo, Brasilia(14, 11)).Should().BeFalse();
    }

    [Fact]
    public void Aceitar_GuardaUsuario_ENaoAceitaDeNovo()
    {
        var convite = Criar();
        var token = convite.GerarToken(Brasilia(6, 11));
        var usuario = Guid.NewGuid();

        convite.Aceitar(token, usuario, Brasilia(6, 12));

        convite.UsuarioId.Should().Be(usuario);
        convite.Pendente.Should().BeFalse();
        var deNovo = () => convite.Aceitar(token, usuario, Brasilia(6, 13));
        deNovo.Should().Throw<EquipeException>();
    }

    [Fact]
    public void Revogar_SoPendente()
    {
        var revogado = Criar();
        var token = revogado.GerarToken(Brasilia(6, 11));
        revogado.Revogar(Brasilia(6, 12));
        var aceito = Criar();
        aceito.Aceitar(aceito.GerarToken(Brasilia(6, 11)), Guid.NewGuid(), Brasilia(6, 12));

        aceito.Revogar(Brasilia(6, 13));

        revogado.Validar(token, Brasilia(6, 13)).Should().BeFalse();
        revogado.RevogadoEm.Should().Be(Brasilia(6, 12));
        aceito.RevogadoEm.Should().BeNull();
    }
}
