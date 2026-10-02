using Acordu.Domain.Common;

namespace Acordu.Domain.UnitTests.Common;

public sealed class EntidadeBaseTests
{
    private sealed record EventoDeTeste : IEventoDominio;

    private sealed class Raiz(Guid id, Guid tenantId) : EntidadeBase(id, tenantId)
    {
        public void Disparar() => Registrar(new EventoDeTeste());
    }

    private sealed class RaizDeCliente(Guid id, Guid tenantId, Guid clienteId) : RaizComCliente(id, tenantId, clienteId);

    [Fact]
    public void Construtor_IdVazio_LancaExcecao()
    {
        var acao = () => new Raiz(Guid.Empty, Guid.NewGuid());

        acao.Should().Throw<ArgumentException>().WithParameterName("id");
    }

    [Fact]
    public void Construtor_TenantVazio_LancaExcecao()
    {
        var acao = () => new Raiz(Guid.NewGuid(), Guid.Empty);

        acao.Should().Throw<ArgumentException>().WithParameterName("tenantId");
    }

    [Fact]
    public void Construtor_ClienteVazio_LancaExcecao()
    {
        var acao = () => new RaizDeCliente(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);

        acao.Should().Throw<ArgumentException>().WithParameterName("clienteId");
    }

    [Fact]
    public void Registrar_AcumulaEventosAteLimpar()
    {
        var raiz = new Raiz(Guid.NewGuid(), Guid.NewGuid());

        raiz.Disparar();
        raiz.Disparar();
        raiz.Eventos.Should().HaveCount(2);

        raiz.LimparEventos();
        raiz.Eventos.Should().BeEmpty();
    }
}
