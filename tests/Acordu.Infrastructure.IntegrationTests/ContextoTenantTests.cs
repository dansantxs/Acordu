using Acordu.Domain.Enums;
using Acordu.Infrastructure.MultiTenancy;

namespace Acordu.Infrastructure.IntegrationTests;

public sealed class ContextoTenantTests
{
    [Fact]
    public void Novo_ComecaSemTenant()
    {
        var contexto = new ContextoTenant();

        contexto.TenantId.Should().BeNull();
        contexto.EhCliente.Should().BeFalse();
    }

    [Fact]
    public void DefinirCliente_AtivaIsolamentoPorCliente()
    {
        var contexto = new ContextoTenant();
        var tenant = Guid.NewGuid();
        var cliente = Guid.NewGuid();

        contexto.DefinirCliente(tenant, cliente);

        contexto.TenantId.Should().Be(tenant);
        contexto.EhCliente.Should().BeTrue();
        contexto.ClienteId.Should().Be(cliente);
    }

    [Fact]
    public void DefinirUsuario_GuardaPapelPermissoesEEscopo()
    {
        var contexto = new ContextoTenant();
        var tenant = Guid.NewGuid();
        var usuario = Guid.NewGuid();
        var cliente = Guid.NewGuid();

        contexto.DefinirUsuario(tenant, usuario, "Cliente", ["portal.solicitacao.ver", "portal.solicitacao.ver"], cliente, EscopoPortal.Proprias);

        contexto.TenantId.Should().Be(tenant);
        contexto.UsuarioId.Should().Be(usuario);
        contexto.Papel.Should().Be("Cliente");
        contexto.Permissoes.Should().BeEquivalentTo(["portal.solicitacao.ver"]);
        contexto.EhCliente.Should().BeTrue();
        contexto.ClienteId.Should().Be(cliente);
        contexto.EscopoPortal.Should().Be(EscopoPortal.Proprias);
    }

    [Fact]
    public void DefinirUsuario_ClienteSemEscopo_Recusa()
    {
        var contexto = new ContextoTenant();

        var acao = () => contexto.DefinirUsuario(Guid.NewGuid(), Guid.NewGuid(), "Cliente", [], Guid.NewGuid(), null);

        acao.Should().Throw<ArgumentException>().WithParameterName("escopo");
        contexto.TenantId.Should().BeNull();
    }

    [Fact]
    public void DefinirEquipe_SemUsuarioNemPermissoes()
    {
        var contexto = new ContextoTenant();

        contexto.DefinirEquipe(Guid.NewGuid());

        contexto.UsuarioId.Should().BeNull();
        contexto.Permissoes.Should().BeEmpty();
    }

    [Fact]
    public void DefinirDuasVezes_Recusa()
    {
        var contexto = new ContextoTenant();
        contexto.DefinirEquipe(Guid.NewGuid());

        var acao = () => contexto.DefinirEquipe(Guid.NewGuid());

        acao.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TenantVazio_Recusa()
    {
        var acao = () => new ContextoTenant().DefinirEquipe(Guid.Empty);

        acao.Should().Throw<ArgumentException>();
    }
}
