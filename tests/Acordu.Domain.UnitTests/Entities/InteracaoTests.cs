using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;
using static Acordu.Domain.UnitTests.Calendarios;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class InteracaoTests
{
    private const long VinteMb = 20L * 1024 * 1024;
    private static readonly Guid Solicitacao = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid Autor = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly PoliticaAnexos Politica = new(VinteMb, ["image/png", "application/pdf"], 5);

    private static NovoAnexo Png(string nome = "arte.png", long tamanho = 1024) =>
        new(nome, "image/png", tamanho, $"tenant/anexos/{Guid.NewGuid()}");

    private static Interacao Registrar(
        string? texto = "Segue a arte revisada.",
        Visibilidade visibilidade = Visibilidade.Externa,
        bool autorEhCliente = false,
        IReadOnlyList<NovoAnexo>? anexos = null,
        CanalMensagem? canal = null) =>
        Interacao.Registrar(
            Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, Solicitacao, Autor, autorEhCliente, texto, visibilidade,
            canal ?? (autorEhCliente ? CanalMensagem.Portal : CanalMensagem.Acordu), anexos ?? [], Politica, Brasilia(6, 11));

    [Fact]
    public void Registrar_ComTexto_GuardaDadosEEmiteEvento()
    {
        var interacao = Registrar();

        interacao.Texto.Should().Be("Segue a arte revisada.");
        interacao.Visibilidade.Should().Be(Visibilidade.Externa);
        interacao.RegistradaEm.Should().Be(Brasilia(6, 11));
        interacao.ClienteId.Should().Be(Contratos.Cliente);
        interacao.Eventos.Should().ContainSingle();
        interacao.Eventos[0].Should().Be(new InteracaoRegistrada(
            interacao.Id, Calendarios.Tenant, Contratos.Cliente, Solicitacao, Autor, false, Visibilidade.Externa, Brasilia(6, 11)));
    }

    [Fact]
    public void Registrar_SoComAnexo_Aceita()
    {
        var interacao = Registrar(texto: null, anexos: [Png()]);

        interacao.Texto.Should().BeNull();
        interacao.Anexos.Should().ContainSingle();
        interacao.Anexos[0].NomeArquivo.Should().Be("arte.png");
        interacao.Anexos[0].Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Registrar_SemTextoNemAnexo_LancaExcecao()
    {
        var acao = () => Registrar(texto: "   ");

        acao.Should().Throw<ArgumentException>().WithParameterName("texto");
    }

    [Fact]
    public void Registrar_ClienteInterna_LancaExcecao()
    {
        var acao = () => Registrar(visibilidade: Visibilidade.Interna, autorEhCliente: true);

        acao.Should().Throw<ArgumentException>().WithParameterName("visibilidade");
    }

    [Fact]
    public void Registrar_EquipeInterna_Aceita()
    {
        Registrar(visibilidade: Visibilidade.Interna).Visibilidade.Should().Be(Visibilidade.Interna);
    }

    [Fact]
    public void Registrar_AlemDoMaximoDeAnexos_LancaExcecao()
    {
        var acao = () => Registrar(anexos: [Png(), Png(), Png(), Png(), Png(), Png()]);

        acao.Should().Throw<AnexoInvalidoException>();
    }

    [Fact]
    public void Registrar_AnexoAcimaDoTamanho_LancaExcecao()
    {
        var acao = () => Registrar(anexos: [Png(tamanho: VinteMb + 1)]);

        acao.Should().Throw<AnexoInvalidoException>();
    }

    [Fact]
    public void Registrar_AnexoNoLimiteDoTamanho_Aceita()
    {
        Registrar(anexos: [Png(tamanho: VinteMb)]).Anexos.Should().ContainSingle();
    }

    [Fact]
    public void Registrar_AnexoVazio_LancaExcecao()
    {
        var acao = () => Registrar(anexos: [Png(tamanho: 0)]);

        acao.Should().Throw<AnexoInvalidoException>();
    }

    [Fact]
    public void Registrar_TipoNaoPermitido_LancaExcecao()
    {
        var acao = () => Registrar(anexos: [new NovoAnexo("virus.exe", "application/x-msdownload", 10, "k")]);

        acao.Should().Throw<AnexoInvalidoException>();
    }

    [Theory]
    [InlineData("../segredo.png")]
    [InlineData("pasta\\arte.png")]
    [InlineData("..")]
    public void Registrar_NomeComCaminho_LancaExcecao(string nome)
    {
        var acao = () => Registrar(anexos: [Png(nome)]);

        acao.Should().Throw<AnexoInvalidoException>();
    }

    [Fact]
    public void Registrar_TipoNormalizado()
    {
        var interacao = Registrar(anexos: [new NovoAnexo("a.pdf", " Application/PDF ", 10, "k")]);

        interacao.Anexos[0].TipoConteudo.Should().Be("application/pdf");
    }

    [Fact]
    public void Registrar_AnexoComIdDefinido_UsaOId()
    {
        var id = Guid.NewGuid();

        var interacao = Registrar(anexos: [new NovoAnexo("a.pdf", "application/pdf", 10, $"t/s/{id}", id)]);

        interacao.Anexos[0].Id.Should().Be(id);
    }

    [Fact]
    public void Registrar_InternaComMencao_EmitePessoasMencionadas()
    {
        var ana = Guid.NewGuid();

        var interacao = Registrar($"@{{{ana}}} pode revisar?", Visibilidade.Interna);

        interacao.Eventos.Should().HaveCount(2);
        var mencao = (PessoasMencionadas)interacao.Eventos[1];
        mencao.Alvo.Should().Be(AlvoColaboracao.Solicitacao);
        mencao.AlvoId.Should().Be(Solicitacao);
        mencao.OrigemId.Should().Be(interacao.Id);
        mencao.AutorId.Should().Be(Autor);
        mencao.Mencionados.Should().Equal(ana);
    }

    [Fact]
    public void Registrar_ExternaDaEquipeComMencao_LancaExcecao()
    {
        var acao = () => Registrar($"@{{{Guid.NewGuid()}}} veja", Visibilidade.Externa);

        acao.Should().Throw<ArgumentException>().WithParameterName("texto");
    }

    [Fact]
    public void Registrar_DoClienteComToken_NaoLeMencao()
    {
        var interacao = Registrar($"@{{{Guid.NewGuid()}}} oi", Visibilidade.Externa, autorEhCliente: true);

        interacao.Eventos.Should().HaveCount(1);
    }

    [Theory] // F15-60
    [InlineData(Visibilidade.Externa)]
    [InlineData(Visibilidade.Interna)]
    public void Registrar_DaEquipePeloAcordu_GuardaOCanal(Visibilidade visibilidade)
    {
        Registrar(visibilidade: visibilidade, canal: CanalMensagem.Acordu).Canal.Should().Be(CanalMensagem.Acordu);
    }

    [Theory] // F15-60, F15-61
    [InlineData(CanalMensagem.Portal)]
    [InlineData(CanalMensagem.Email)]
    [InlineData(CanalMensagem.Whatsapp)]
    public void Registrar_DoContatoPorCanalDeFora_GuardaOCanal(CanalMensagem canal)
    {
        Registrar(autorEhCliente: true, canal: canal).Canal.Should().Be(canal);
    }

    [Theory] // ADR 0016, item 6
    [InlineData(CanalMensagem.Portal)]
    [InlineData(CanalMensagem.Email)]
    [InlineData(CanalMensagem.Whatsapp)]
    public void Registrar_DaEquipeForaDoAcordu_LancaExcecao(CanalMensagem canal)
    {
        var acao = () => Registrar(canal: canal);

        acao.Should().Throw<ArgumentException>().WithParameterName(nameof(canal));
    }

    [Fact] // ADR 0016, item 6
    public void Registrar_DoContatoPeloAcordu_LancaExcecao()
    {
        var acao = () => Registrar(autorEhCliente: true, canal: CanalMensagem.Acordu);

        acao.Should().Throw<ArgumentException>().WithParameterName("canal");
    }

    [Fact]
    public void Registrar_CanalForaDoEnum_LancaExcecao()
    {
        var acao = () => Registrar(canal: (CanalMensagem)99);

        acao.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("canal");
    }

    private static Interacao NotaDoSistema(
        string texto = "Anexos do e-mail que não entraram na solicitação:\n- a.exe: tipo de arquivo não permitido",
        CanalMensagem canal = CanalMensagem.Email,
        Guid? autor = null) =>
        Interacao.RegistrarNotaDoSistema(
            Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, Solicitacao, autor ?? AutoresDoSistema.EntradaEmail, texto, canal,
            Brasilia(6, 11));

    [Fact] // F15-15, F15-98; design D8 e D15: INTERNA, autor do sistema, canal da entrada
    public void RegistrarNotaDoSistema_DaEntradaEmail_InternaDoAutorDoSistemaNoCanalEmail()
    {
        var nota = NotaDoSistema();

        nota.Visibilidade.Should().Be(Visibilidade.Interna);
        nota.AutorId.Should().Be(AutoresDoSistema.EntradaEmail);
        nota.AutorEhCliente.Should().BeFalse();
        nota.Canal.Should().Be(CanalMensagem.Email, "a exceção única à regra da equipe só no canal Acordu");
        nota.SolicitacaoId.Should().Be(Solicitacao);
        nota.Texto.Should().StartWith("Anexos do e-mail");
        nota.Anexos.Should().BeEmpty();
        nota.RegistradaEm.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact] // F15-98: sem evento, nada de aviso ao contato nem aos seguidores, feed ou seguimento
    public void RegistrarNotaDoSistema_QualquerUma_NaoRegistraEvento()
    {
        NotaDoSistema().Eventos.Should().BeEmpty();
    }

    [Fact] // design D15: a nota é do sistema, nunca de uma pessoa
    public void RegistrarNotaDoSistema_AutorQueNaoEDoSistema_LancaExcecao()
    {
        var acao = () => NotaDoSistema(autor: Autor);

        acao.Should().Throw<ArgumentException>().WithParameterName("autorId");
    }

    [Fact] // design D8: a nota leva o canal da entrada que a gerou
    public void RegistrarNotaDoSistema_CanalAcordu_LancaExcecao()
    {
        var acao = () => NotaDoSistema(canal: CanalMensagem.Acordu);

        acao.Should().Throw<ArgumentException>().WithParameterName("canal");
    }

    [Fact]
    public void RegistrarNotaDoSistema_SemTexto_LancaExcecao()
    {
        var acao = () => NotaDoSistema(texto: "  ");

        acao.Should().Throw<ArgumentException>().WithParameterName("texto");
    }

    [Fact] // design D15: o autor do sistema não escreve pelo caminho das pessoas
    public void Registrar_AutorDoSistema_LancaExcecao()
    {
        var acao = () => Interacao.Registrar(
            Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, Solicitacao, AutoresDoSistema.EntradaEmail, false, "oi",
            Visibilidade.Interna, CanalMensagem.Acordu, [], Politica, Brasilia(6, 11));

        acao.Should().Throw<ArgumentException>().WithParameterName("autorId");
    }
}
