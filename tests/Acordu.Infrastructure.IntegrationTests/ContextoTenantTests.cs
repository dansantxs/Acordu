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
