using Acordu.Application.Auditoria;
using Acordu.Application.Common;
using Acordu.Application.Exemplos;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Exceptions;

namespace Acordu.Application.UnitTests.Exemplos;

/// <summary>
/// Spec do F14, F14-91 a F14-95 (design D13, ADR 0015): quando a carga dos exemplos é oferecida, quem pode carregar e
/// apagar, e as recusas sem gravar nada.
/// </summary>
public sealed class ComandosExemplosTests
{
    private static readonly Guid Dono = Guid.NewGuid();
    private readonly ExemplosFalsos _exemplos = new();
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly UnidadeFalsa _unidade = new();
    private CargaEmMemoria? _memoria;

    private ComandosExemplos Comandos(Tenant tenant, ArmazenamentoFalso? armazenamento = null, bool comPermissao = true)
    {
        _memoria = new CargaEmMemoria(tenant.Id);
        string[] permissoes = comPermissao ? [Permissoes.ConfiguracaoGerenciar] : [Permissoes.ClienteGerenciar];
        return new ComandosExemplos(
            new ContextoDoDono(tenant.Id, Dono, permissoes),
            new TenantFixo(tenant),
            _exemplos,
            _memoria.Carga,
            _auditoria,
            _unidade,
            armazenamento ?? new ArmazenamentoFalso());
    }

    private static Tenant Vencida()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio Traço", Falsos.Agora.AddDays(-31), Dono, TimeSpan.FromDays(30));
        tenant.Vencer(Falsos.Agora);
        return tenant;
    }

    private static Tenant Ativa()
    {
        var tenant = Falsos.EmTeste(Dono);
        tenant.Ativar(Falsos.Agora);
        return tenant;
    }

    [Fact]
    public async Task ObterAsync_EmTesteSemDados_Disponivel() // F14-91
    {
        var estado = await Comandos(Falsos.EmTeste(Dono)).ObterAsync();

        estado.Should().Be(new DadosExemploDto(Disponivel: true, Carregados: false));
    }

    [Fact]
    public async Task ObterAsync_ComDadosProprios_Indisponivel() // F14-93
    {
        _exemplos.Situacao = new SituacaoDosExemplos(Carregados: false, DadosProprios: true);

        var estado = await Comandos(Falsos.EmTeste(Dono)).ObterAsync();

        estado.Disponivel.Should().BeFalse();
    }

    [Fact]
    public async Task ObterAsync_ComExemplos_OfereceApagarENaoCarregarDeNovo() // F14-94
    {
        _exemplos.Situacao = new SituacaoDosExemplos(Carregados: true, DadosProprios: false);

        var estado = await Comandos(Falsos.EmTeste(Dono)).ObterAsync();

        estado.Should().Be(new DadosExemploDto(Disponivel: false, Carregados: true));
    }

    [Fact]
    public async Task ObterAsync_EmpresaAtiva_Indisponivel()
    {
        var estado = await Comandos(Ativa()).ObterAsync();

        estado.Disponivel.Should().BeFalse("os exemplos são da empresa em teste (RF-EMP-05)");
    }

    [Fact]
    public async Task CarregarAsync_Disponivel_MontaAuditaEGravaUmaVez() // F14-92
    {
        var tenant = Falsos.EmTeste(Dono);
        var comandos = Comandos(tenant);

        await comandos.CarregarAsync();

        _memoria!.Clientes.Itens.Should().HaveCount(DadosFicticios.Arquivo.Clientes.Count).And.OnlyContain(c => c.Exemplo);
        _unidade.Gravacoes.Should().Be(1);
        _auditoria.Registros.Should().ContainSingle(
            r => r.Acao == AcoesAuditoria.ExemplosCarga && r.AlvoTipo == "empresa" && r.AlvoId == tenant.Id);
    }

    [Fact]
    public async Task CarregarAsync_ComDadosProprios_RecusaSemCarregar() // F14-93
    {
        _exemplos.Situacao = new SituacaoDosExemplos(Carregados: false, DadosProprios: true);
        var comandos = Comandos(Falsos.EmTeste(Dono));

        var carregar = () => comandos.CarregarAsync();

        (await carregar.Should().ThrowAsync<TransicaoInvalidaException>()).WithMessage(ComandosExemplos.MensagemIndisponivel);
        _memoria!.Todas.Should().BeEmpty();
        _unidade.Gravacoes.Should().Be(0);
        _auditoria.Registros.Should().BeEmpty();
    }

    [Fact]
    public async Task CarregarAsync_ExemplosJaCarregados_RecusaSemDuplicar()
    {
        _exemplos.Situacao = new SituacaoDosExemplos(Carregados: true, DadosProprios: false);
        var comandos = Comandos(Falsos.EmTeste(Dono));

        var carregar = () => comandos.CarregarAsync();

        await carregar.Should().ThrowAsync<TransicaoInvalidaException>();
        _memoria!.Todas.Should().BeEmpty();
    }

    [Fact]
    public async Task CarregarAsync_EmpresaAtiva_Recusa()
    {
        var comandos = Comandos(Ativa());

        var carregar = () => comandos.CarregarAsync();

        await carregar.Should().ThrowAsync<TransicaoInvalidaException>();
        _unidade.Gravacoes.Should().Be(0);
    }

    [Fact]
    public async Task CarregarAsync_EmpresaVencida_RecusaComoSoLeitura() // ADR 0015
    {
        var comandos = Comandos(Vencida());

        var carregar = () => comandos.CarregarAsync();

        await carregar.Should().ThrowAsync<EmpresaSomenteLeituraException>();
        _exemplos.Consultas.Should().Be(0, "a recusa da empresa só leitura vem antes da regra dos exemplos");
        _unidade.Gravacoes.Should().Be(0);
    }

    [Fact]
    public async Task CarregarAsync_SemConfiguracaoGerenciar_Recusa() // F14-95
    {
        var comandos = Comandos(Falsos.EmTeste(Dono), comPermissao: false);

        var carregar = () => comandos.CarregarAsync();

        await carregar.Should().ThrowAsync<AcessoNegadoException>();
        _exemplos.Consultas.Should().Be(0);
    }

    [Fact]
    public async Task ObterAsync_SemConfiguracaoGerenciar_Recusa() // F14-95
    {
        var comandos = Comandos(Falsos.EmTeste(Dono), comPermissao: false);

        var obter = () => comandos.ObterAsync();

        await obter.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task ApagarAsync_ComExemplos_AuditaGravaERemoveOsAnexosDepois() // F14-94
    {
        var tenant = Ativa();
        _exemplos.Removidos = new ExemplosRemovidos(4, ["t/anexo-1", "t/anexo-2"]);
        var armazenamento = new ArmazenamentoFalso();
        var comandos = Comandos(tenant, armazenamento);

        await comandos.ApagarAsync();

        _exemplos.Remocoes.Should().Be(1);
        _unidade.Gravacoes.Should().Be(1);
        _auditoria.Registros.Should().ContainSingle(r => r.Acao == AcoesAuditoria.ExemplosRemocao && r.AlvoId == tenant.Id);
        armazenamento.Removidas.Should().Equal("t/anexo-1", "t/anexo-2");
    }

    [Fact]
    public async Task ApagarAsync_StorageFalha_ARemocaoGravadaFica()
    {
        _exemplos.Removidos = new ExemplosRemovidos(1, ["t/anexo-1"]);
        var comandos = Comandos(Falsos.EmTeste(Dono), new ArmazenamentoFalso(falhar: true));

        await comandos.ApagarAsync();

        _unidade.Gravacoes.Should().Be(1);
    }

    [Fact]
    public async Task ApagarAsync_SemExemplos_NaoGravaNemAudita()
    {
        var comandos = Comandos(Falsos.EmTeste(Dono));

        await comandos.ApagarAsync();

        _unidade.Gravacoes.Should().Be(0);
        _auditoria.Registros.Should().BeEmpty();
    }

    [Fact]
    public async Task ApagarAsync_EmpresaVencida_RecusaSemApagar() // ADR 0015
    {
        var comandos = Comandos(Vencida());

        var apagar = () => comandos.ApagarAsync();

        await apagar.Should().ThrowAsync<EmpresaSomenteLeituraException>();
        _exemplos.Remocoes.Should().Be(0, "a remoção passa em parte fora do change tracker: a situação é conferida antes");
    }

    [Fact]
    public async Task ApagarAsync_SemConfiguracaoGerenciar_Recusa() // F14-95
    {
        var comandos = Comandos(Falsos.EmTeste(Dono), comPermissao: false);

        var apagar = () => comandos.ApagarAsync();

        await apagar.Should().ThrowAsync<AcessoNegadoException>();
        _exemplos.Remocoes.Should().Be(0);
    }
}
