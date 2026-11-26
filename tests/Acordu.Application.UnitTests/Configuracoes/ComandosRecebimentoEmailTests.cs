using Acordu.Application.Auditoria;
using Acordu.Application.Common;
using Acordu.Application.Configuracoes;
using Acordu.Application.EntradaEmail;
using Acordu.Application.UnitTests.Exemplos;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Exceptions;

namespace Acordu.Application.UnitTests.Configuracoes;

/// <summary>
/// Spec do F15, F15b (F15-03, F15-04, F15-06, F15-07, F15-93, F15-94, F15-96; design D9 e §1.2): ligar e desligar o
/// recebimento por e-mail, a ordem das recusas e a trilha.
/// </summary>
public sealed class ComandosRecebimentoEmailTests
{
    private const string Dominio = "entrada.acordu.app.br";
    private static readonly Guid Dono = Guid.NewGuid();
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly UnidadeFalsa _unidade = new();

    private ComandosRecebimentoEmail Comandos(Tenant tenant, string? dominio = Dominio, bool comPermissao = true)
    {
        string[] permissoes = comPermissao ? [Permissoes.ConfiguracaoGerenciar] : [Permissoes.SolicitacaoCriar];
        return new ComandosRecebimentoEmail(
            new ContextoDoDono(tenant.Id, Dono, permissoes),
            new TenantFixo(tenant),
            OpcoesEmailEntrada.DaConfiguracao(dominio),
            _auditoria,
            _unidade);
    }

    private static Tenant Vencida()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio Traço", Falsos.Agora.AddDays(-31), Dono, TimeSpan.FromDays(30));
        tenant.Vencer(Falsos.Agora);
        return tenant;
    }

    private static Tenant Demonstracao() => Tenant.CriarDemonstracao(Guid.NewGuid(), "Estúdio Aurora", Falsos.Agora, Dono);

    [Fact]
    public async Task AlterarAsync_LigarDesligado_LigaGravaEAudita() // F15-03, F15-93
    {
        var tenant = Falsos.EmTeste(Dono);

        var resposta = await Comandos(tenant).AlterarAsync(new RecebimentoEmailRequest(true));

        resposta.Should().Be(new RecebimentoEmailDto($"{tenant.CodigoEntrada}@{Dominio}", true));
        tenant.RecebimentoEmail.Should().BeTrue();
        _unidade.Gravacoes.Should().Be(1);
        var registro = _auditoria.Registros.Should().ContainSingle().Subject;
        registro.Acao.Should().Be(AcoesAuditoria.ConfiguracaoRecebimentoEmail);
        registro.AlvoTipo.Should().Be("empresa");
        registro.AlvoId.Should().Be(tenant.Id);
        registro.Dados.Should().BeEquivalentTo(new { antes = new { recebimentoEmail = false }, depois = new { recebimentoEmail = true } });
    }

    [Fact]
    public async Task AlterarAsync_DesligarLigado_DesligaGravaEAudita() // F15-04, F15-93
    {
        var tenant = Falsos.EmTeste(Dono);
        tenant.LigarRecebimentoEmail();

        var resposta = await Comandos(tenant).AlterarAsync(new RecebimentoEmailRequest(false));

        resposta.RecebimentoEmail.Should().BeFalse();
        resposta.EnderecoEntrada.Should().Be($"{tenant.CodigoEntrada}@{Dominio}", "desligar não tira o endereço");
        _unidade.Gravacoes.Should().Be(1);
        _auditoria.Registros.Should().ContainSingle().Which.Dados.Should().BeEquivalentTo(
            new { antes = new { recebimentoEmail = true }, depois = new { recebimentoEmail = false } });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AlterarAsync_JaNoEstadoPedido_RespondeSemGravarNemAuditar(bool ligado)
    {
        var tenant = Falsos.EmTeste(Dono);
        if (ligado)
            tenant.LigarRecebimentoEmail();

        var resposta = await Comandos(tenant).AlterarAsync(new RecebimentoEmailRequest(ligado));

        resposta.RecebimentoEmail.Should().Be(ligado);
        _unidade.Gravacoes.Should().Be(0);
        _auditoria.Registros.Should().BeEmpty();
    }

    [Fact]
    public async Task AlterarAsync_SemPermissao_RecusaSemMudar() // F15-06
    {
        var tenant = Falsos.EmTeste(Dono);

        var alterar = () => Comandos(tenant, comPermissao: false).AlterarAsync(new RecebimentoEmailRequest(true));

        await alterar.Should().ThrowAsync<AcessoNegadoException>();
        tenant.RecebimentoEmail.Should().BeFalse();
        _unidade.Gravacoes.Should().Be(0);
    }

    [Fact]
    public async Task AlterarAsync_SemLigado_ErroNoCampo()
    {
        var tenant = Falsos.EmTeste(Dono);

        var alterar = () => Comandos(tenant).AlterarAsync(new RecebimentoEmailRequest(null));

        (await alterar.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be(ComandosRecebimentoEmail.CampoLigado);
        _unidade.Gravacoes.Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AlterarAsync_EmpresaSoLeitura_RecusaComoSoLeituraSemMudar(bool ligado) // F15-07
    {
        var tenant = Vencida();

        var alterar = () => Comandos(tenant).AlterarAsync(new RecebimentoEmailRequest(ligado));

        await alterar.Should().ThrowAsync<EmpresaSomenteLeituraException>();
        tenant.RecebimentoEmail.Should().BeFalse();
        _unidade.Gravacoes.Should().Be(0);
        _auditoria.Registros.Should().BeEmpty();
    }

    [Fact]
    public async Task AlterarAsync_SoLeituraSemPermissao_PermissaoAntes() // F15-07: depois da permissão
    {
        var alterar = () => Comandos(Vencida(), comPermissao: false).AlterarAsync(new RecebimentoEmailRequest(true));

        await alterar.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task AlterarAsync_LigarSemDominio_TransicaoInvalidaSemMudar() // F15-94
    {
        var tenant = Falsos.EmTeste(Dono);

        var alterar = () => Comandos(tenant, dominio: null).AlterarAsync(new RecebimentoEmailRequest(true));

        (await alterar.Should().ThrowAsync<TransicaoInvalidaException>()).WithMessage(Tenant.MensagemRecebimentoIndisponivel);
        tenant.RecebimentoEmail.Should().BeFalse();
        _unidade.Gravacoes.Should().Be(0);
        _auditoria.Registros.Should().BeEmpty();
    }

    [Fact]
    public async Task AlterarAsync_LigarSemDominioSemPermissao_PermissaoAntes() // F15-94: depois da permissão
    {
        var alterar = () => Comandos(Falsos.EmTeste(Dono), dominio: null, comPermissao: false)
            .AlterarAsync(new RecebimentoEmailRequest(true));

        await alterar.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task AlterarAsync_DesligarSemDominio_RespondeSemEndereco() // F15-94
    {
        var tenant = Falsos.EmTeste(Dono);

        var resposta = await Comandos(tenant, dominio: null).AlterarAsync(new RecebimentoEmailRequest(false));

        resposta.Should().Be(new RecebimentoEmailDto(null, false));
    }

    [Fact]
    public async Task AlterarAsync_LigarNaDemonstracao_TransicaoInvalidaSemMudar() // F15-96
    {
        var tenant = Demonstracao();

        var alterar = () => Comandos(tenant).AlterarAsync(new RecebimentoEmailRequest(true));

        await alterar.Should().ThrowAsync<TransicaoInvalidaException>();
        tenant.RecebimentoEmail.Should().BeFalse();
        _unidade.Gravacoes.Should().Be(0);
        _auditoria.Registros.Should().BeEmpty();
    }
}
