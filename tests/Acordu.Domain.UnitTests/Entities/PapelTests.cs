using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class PapelTests
{
    private static Papel Padrao(string nome) => Papel.Padroes(Calendarios.Tenant).Single(p => p.Nome == nome);

    [Fact]
    public void Padroes_QuatroDoTenantMarcadosComoPadrao()
    {
        var padroes = Papel.Padroes(Calendarios.Tenant);

        padroes.Select(p => p.Nome).Should().Equal(Papel.NomeDono, Papel.NomeMembro, Papel.NomeLeitor, Papel.NomeCliente);
        padroes.Should().OnlyContain(p => p.Padrao && p.TenantId == Calendarios.Tenant);
        padroes.Select(p => p.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Dono_TodasAsInternas()
    {
        var dono = Padrao(Papel.NomeDono);

        dono.Tipo.Should().Be(TipoPapel.Interno);
        dono.Permissoes.Should().BeEquivalentTo(Permissoes.Internas);
    }

    [Fact]
    public void Membro_InternasMenosAsDeGestao()
    {
        var membro = Padrao(Papel.NomeMembro);

        membro.Permissoes.Should().BeEquivalentTo(Permissoes.Internas.Except(
        [
            Permissoes.ContratoGerenciar, Permissoes.ContratoAjustarSaldo, Permissoes.ContratoFecharPeriodo,
            Permissoes.ApontamentoEditarTodos, Permissoes.ConfiguracaoGerenciar, Permissoes.UsuarioConvidar,
            Permissoes.UsuarioRemover, Permissoes.PapelGerenciar, Permissoes.AuditoriaVer,
        ]));
        membro.Possui(Permissoes.PortalGerenciarAcessos).Should().BeTrue();
    }

    [Fact]
    public void Leitor_SoAsDeVerIncluindoDashboard_SemAuditoria()
    {
        var leitor = Padrao(Papel.NomeLeitor);

        leitor.Permissoes.Should().BeEquivalentTo(
        [
            Permissoes.ClienteVer, Permissoes.ContratoVer, Permissoes.SolicitacaoVer, Permissoes.TarefaVer,
            Permissoes.ProjetoVer, Permissoes.ApontamentoVer, Permissoes.RecorrenciaVer, Permissoes.DashboardVer,
        ]);
        leitor.Permissoes.Should().NotContain(Permissoes.AuditoriaVer);
    }

    [Fact]
    public void Cliente_TodasAsDoPortal()
    {
        var cliente = Padrao(Papel.NomeCliente);

        cliente.Tipo.Should().Be(TipoPapel.Cliente);
        cliente.Permissoes.Should().BeEquivalentTo(Permissoes.DoPortal);
    }

    [Fact]
    public void Criar_SobMedida_OrdenaERemoveDuplicadas()
    {
        var papel = Papel.Criar(
            Guid.NewGuid(), Calendarios.Tenant, " Financeiro ", TipoPapel.Interno,
            [Permissoes.ContratoVer, Permissoes.ClienteVer, Permissoes.ContratoVer]);

        papel.Nome.Should().Be("Financeiro");
        papel.Padrao.Should().BeFalse();
        papel.Permissoes.Should().Equal(Permissoes.ClienteVer, Permissoes.ContratoVer);
        papel.Possui(Permissoes.ContratoVer).Should().BeTrue();
        papel.Possui(Permissoes.ContratoGerenciar).Should().BeFalse();
    }

    [Fact]
    public void Criar_InternoComPermissaoDoPortal_LancaExcecao()
    {
        var acao = () => Papel.Criar(
            Guid.NewGuid(), Calendarios.Tenant, "Misto", TipoPapel.Interno, [Permissoes.ClienteVer, Permissoes.PortalExtratoVer]);

        acao.Should().Throw<ArgumentException>().WithParameterName("permissoes");
    }

    [Fact]
    public void Criar_ClienteComPermissaoInterna_LancaExcecao()
    {
        // portal.gerenciar_acessos começa com "portal." mas é interna: a regra é pelo catálogo, não pelo prefixo.
        var acao = () => Papel.Criar(
            Guid.NewGuid(), Calendarios.Tenant, "Contato", TipoPapel.Cliente, [Permissoes.PortalGerenciarAcessos]);

        acao.Should().Throw<ArgumentException>().WithParameterName("permissoes");
    }

    [Fact]
    public void Criar_PermissaoForaDoCatalogo_LancaExcecao()
    {
        var acao = () => Papel.Criar(Guid.NewGuid(), Calendarios.Tenant, "X", TipoPapel.Interno, ["cliente.apagar"]);

        acao.Should().Throw<ArgumentException>().WithParameterName("permissoes");
    }

    [Fact]
    public void Criar_SemPermissoes_LancaExcecao()
    {
        var acao = () => Papel.Criar(Guid.NewGuid(), Calendarios.Tenant, "Vazio", TipoPapel.Interno, []);

        acao.Should().Throw<ArgumentException>().WithParameterName("permissoes");
    }

    [Fact]
    public void DefinirPermissoes_SobMedida_Substitui()
    {
        var papel = Papel.Criar(Guid.NewGuid(), Calendarios.Tenant, "Financeiro", TipoPapel.Interno, [Permissoes.ContratoVer]);

        papel.DefinirPermissoes([Permissoes.ContratoGerenciar]);

        papel.Permissoes.Should().Equal(Permissoes.ContratoGerenciar);
    }

    [Fact]
    public void Padrao_NaoPodeSerAlterado()
    {
        var membro = Padrao(Papel.NomeMembro);

        var renomear = () => membro.Renomear("Equipe");
        var redefinir = () => membro.DefinirPermissoes([Permissoes.ContratoGerenciar]);

        renomear.Should().Throw<TransicaoInvalidaException>();
        redefinir.Should().Throw<TransicaoInvalidaException>();
        membro.Nome.Should().Be(Papel.NomeMembro);
        membro.Possui(Permissoes.ContratoGerenciar).Should().BeFalse();
    }
}
