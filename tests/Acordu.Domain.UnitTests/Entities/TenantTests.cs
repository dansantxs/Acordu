using Acordu.Domain.Common;
using Acordu.Domain.Entities;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class TenantTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 6, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Criar_NomeAparadoEDataInformada()
    {
        var id = Guid.NewGuid();

        var tenant = Tenant.Criar(id, "  Estúdio Traço ", Agora);

        tenant.Id.Should().Be(id);
        tenant.Nome.Should().Be("Estúdio Traço");
        tenant.CriadoEm.Should().Be(Agora);
    }

    [Fact]
    public void Criar_InstanteGuardadoEmUtc()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.FromHours(-3)));

        tenant.CriadoEm.Offset.Should().Be(TimeSpan.Zero);
        tenant.CriadoEm.Should().Be(Agora);
    }

    [Fact]
    public void Criar_IdVazio_LancaExcecao()
    {
        var acao = () => Tenant.Criar(Guid.Empty, "Estúdio", Agora);

        acao.Should().Throw<ArgumentException>().WithParameterName("id");
    }

    [Fact]
    public void Renomear_AcimaDoLimite_MantemONome()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora);

        var acao = () => tenant.Renomear(new string('x', LimitesTexto.Nome + 1));

        acao.Should().Throw<ArgumentException>().WithParameterName("nome");
        tenant.Nome.Should().Be("Estúdio");
    }
}
