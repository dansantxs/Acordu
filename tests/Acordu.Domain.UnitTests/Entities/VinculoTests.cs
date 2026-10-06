using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class VinculoTests
{
    private static readonly Guid Usuario = Guid.NewGuid();
    private static readonly Guid ClienteId = Guid.NewGuid();

    private static Papel Padrao(string nome) => Papel.Padroes(Calendarios.Tenant).Single(p => p.Nome == nome);

    [Fact]
    public void Criar_Interno_SemClienteNemEscopo()
    {
        var dono = Padrao(Papel.NomeDono);

        var vinculo = Vinculo.Criar(Guid.NewGuid(), Usuario, dono);

        vinculo.TenantId.Should().Be(Calendarios.Tenant);
        vinculo.UsuarioId.Should().Be(Usuario);
        vinculo.PapelId.Should().Be(dono.Id);
        vinculo.ClienteId.Should().BeNull();
        vinculo.EscopoPortal.Should().BeNull();
    }

    [Fact]
    public void Criar_Cliente_ComClienteEEscopo()
    {
        var vinculo = Vinculo.Criar(Guid.NewGuid(), Usuario, Padrao(Papel.NomeCliente), ClienteId, EscopoPortal.Proprias);

        vinculo.ClienteId.Should().Be(ClienteId);
        vinculo.EscopoPortal.Should().Be(EscopoPortal.Proprias);
    }

    [Theory]
    [InlineData(false, true, "clienteId")]
    [InlineData(true, false, "escopo")]
    public void Criar_ClienteIncompleto_LancaExcecao(bool comCliente, bool comEscopo, string parametro)
    {
        var acao = () => Vinculo.Criar(
            Guid.NewGuid(), Usuario, Padrao(Papel.NomeCliente),
            comCliente ? ClienteId : null, comEscopo ? EscopoPortal.TodasDoCliente : null);

        acao.Should().Throw<ArgumentException>().WithParameterName(parametro);
    }

    [Theory]
    [InlineData(true, false, "clienteId")]
    [InlineData(false, true, "escopo")]
    public void Criar_InternoComDadosDeCliente_LancaExcecao(bool comCliente, bool comEscopo, string parametro)
    {
        var acao = () => Vinculo.Criar(
            Guid.NewGuid(), Usuario, Padrao(Papel.NomeMembro),
            comCliente ? ClienteId : null, comEscopo ? EscopoPortal.TodasDoCliente : null);

        acao.Should().Throw<ArgumentException>().WithParameterName(parametro);
    }

    [Fact]
    public void Criar_UsuarioVazio_LancaExcecao()
    {
        var acao = () => Vinculo.Criar(Guid.NewGuid(), Guid.Empty, Padrao(Papel.NomeDono));

        acao.Should().Throw<ArgumentException>().WithParameterName("usuarioId");
    }

    [Fact]
    public void TrocarPapel_DeClienteParaInterno_LimpaClienteEEscopo()
    {
        var papeis = Papel.Padroes(Calendarios.Tenant);
        var vinculo = Vinculo.Criar(Guid.NewGuid(), Usuario, papeis[3], ClienteId, EscopoPortal.Proprias);

        vinculo.TrocarPapel(papeis[1]);

        vinculo.PapelId.Should().Be(papeis[1].Id);
        vinculo.ClienteId.Should().BeNull();
        vinculo.EscopoPortal.Should().BeNull();
    }

    [Fact]
    public void TrocarPapel_DeOutroTenant_LancaExcecaoEMantem()
    {
        var dono = Padrao(Papel.NomeDono);
        var vinculo = Vinculo.Criar(Guid.NewGuid(), Usuario, dono);
        var deOutroTenant = Papel.Padroes(Guid.NewGuid())[1];

        var acao = () => vinculo.TrocarPapel(deOutroTenant);

        acao.Should().Throw<ArgumentException>().WithParameterName("papel");
        vinculo.PapelId.Should().Be(dono.Id);
    }

    [Fact]
    public void TrocarPapel_ParaClienteIncompleto_LancaExcecaoEMantem()
    {
        var papeis = Papel.Padroes(Calendarios.Tenant);
        var vinculo = Vinculo.Criar(Guid.NewGuid(), Usuario, papeis[1]);

        var acao = () => vinculo.TrocarPapel(papeis[3], ClienteId);

        acao.Should().Throw<ArgumentException>().WithParameterName("escopo");
        vinculo.PapelId.Should().Be(papeis[1].Id);
        vinculo.ClienteId.Should().BeNull();
    }

    [Fact]
    public void AlterarEscopo_Cliente_Altera()
    {
        var vinculo = Vinculo.Criar(Guid.NewGuid(), Usuario, Padrao(Papel.NomeCliente), ClienteId, EscopoPortal.Proprias);

        vinculo.AlterarEscopo(EscopoPortal.TodasDoCliente);

        vinculo.EscopoPortal.Should().Be(EscopoPortal.TodasDoCliente);
    }

    [Fact]
    public void AlterarEscopo_Interno_LancaExcecao()
    {
        var vinculo = Vinculo.Criar(Guid.NewGuid(), Usuario, Padrao(Papel.NomeDono));

        var acao = () => vinculo.AlterarEscopo(EscopoPortal.TodasDoCliente);

        acao.Should().Throw<ArgumentException>().WithParameterName("escopo");
    }
}
