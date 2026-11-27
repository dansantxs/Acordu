using Acordu.Application.Abstractions;
using Acordu.Application.Anexos;
using Acordu.Application.Common;
using Acordu.Application.Contratos;
using Acordu.Application.EntradaEmail;
using Acordu.Application.Solicitacoes;
using Acordu.Application.UnitTests.Exemplos;
using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.UnitTests.EntradaEmail;

/// <summary>
/// Design do F15, D6 e §3 (ADR 0016, item 8): o e-mail aceito para a empresa deixa um registro de entrada, sem conteúdo,
/// e a repetição da mesma entrega não grava de novo; o automático, o laço e o filtro (F15e) dão o desfecho do descarte.
/// F15f (F15-10 a F15-13, F15-27; design §1.2 e D4): o e-mail novo de remetente conhecido abre a solicitação de origem
/// EMAIL, com a conversa no fio do e-mail; a resposta à conversa fica só no registro até o F15i. F15g (F15-14, F15-15;
/// design D14 e D15): os anexos um por um, a nota do sistema dos recusados e a lista deles no registro. F15h (F15-20,
/// F15-24 a F15-26, F15-70, F15-103; design D10): o não identificado vai à triagem, com o motivo e os anexos na chave da
/// triagem, uma vez só por entrega.
/// </summary>
public sealed class ComandosEntradaEmailTests
{
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly DestinoDeEntrada Destino = new("k7m2qd9w@entrada.acordu.app.br", "k7m2qd9w", null);
    private readonly Repositorio<EmailRecebido> _repositorio = new();
    private readonly Repositorio<Solicitacao> _solicitacoes = new();
    private readonly Repositorio<Interacao> _interacoes = new();
    private readonly Repositorio<ConversaPorEmail> _conversas = new();
    private readonly Repositorio<ItemTriagemEmail> _triagem = new();
    private readonly ContatosEmMemoria _contatos = new();
    private readonly UnidadeFalsa _unidade = new();
    private readonly ArmazenamentoFalso _armazenamento = new();
    private int _protocolo = 41;

    private ComandosEntradaEmail Comandos(
        ITenantContext? contexto = null, IUnitOfWork? unidade = null, IArmazenamentoArquivos? armazenamento = null)
    {
        var calendarios = new CalendariosFixos(Empresa);
        var abertura = new AberturaPeloContato(
            _solicitacoes, new Repositorio<Contrato>(), _interacoes, new SemContratos(), new Protocolo(() => ++_protocolo),
            calendarios, new RelogioDoTenant(calendarios, new RelogioFixo(Falsos.Agora)));
        return new(
            contexto ?? new ContextoDoDono(Empresa, Guid.NewGuid()),
            new RegistrosEmMemoria(_repositorio),
            _repositorio,
            _contatos,
            abertura,
            new RecebimentoAnexos(armazenamento ?? _armazenamento),
            _interacoes,
            _conversas,
            _triagem,
            unidade ?? _unidade,
            new RelogioFixo(Falsos.Agora),
            OpcoesEmailEntrada.DaConfiguracao("entrada.acordu.app.br", remetenteDosAvisos: "nao-responda@send.acordu.app.br"));
    }

    private static MensagemDeEntrada Mensagem(string? messageId = "abc@mail.exemplo.com") =>
        new(messageId, ["ana@cliente.com.br"], new string('a', 64));

    /// <summary>Um e-mail de remetente conhecido: um contato só, autenticidade confirmada.</summary>
    private static MensagemDeEntrada DoContato(string? messageId = "abc@mail.exemplo.com") =>
        Mensagem(messageId) with
        {
            Remetentes = ["Ana@Cliente.com.br"],
            Assunto = "RE: Fwd: Forno parou de novo",
            Texto = "O forno parou de novo hoje cedo.\r\n",
            Autenticidade = "pass",
        };

    private ContatoDeEntrada ContatoConhecido(bool clienteAtivo = true, bool contatoAtivo = true, Guid? usuario = null)
    {
        var contato = new ContatoDeEntrada(Guid.NewGuid(), Guid.NewGuid(), usuario, clienteAtivo, contatoAtivo);
        _contatos.Adicionar("ana@cliente.com.br", contato);
        return contato;
    }

    [Fact] // o remetente não é contato: o registro vai sem conteúdo, apontando o item da triagem (F15h)
    public async Task ReceberAsync_Novo_RegistraTriagemSemConteudo()
    {
        var desfecho = await Comandos().ReceberAsync(Mensagem(), Destino);

        desfecho.Should().Be(DesfechoEntrada.Triagem);
        _unidade.Gravacoes.Should().Be(1);
        var registro = _repositorio.Itens.Should().ContainSingle().Subject;
        registro.TenantId.Should().Be(Empresa);
        registro.Resultado.Should().Be(ResultadoEntradaEmail.Triagem);
        registro.ItemTriagemId.Should().Be(_triagem.Itens.Single().Id);
        registro.RecebidoEm.Should().Be(Falsos.Agora);
        registro.ChaveEntrada.Should().Be(ChaveDeEntrada.Calcular("abc@mail.exemplo.com", Destino.Endereco, new string('a', 64)));
        registro.RemetenteHash.Should().Be(ChaveDeEntrada.DoRemetente(["ana@cliente.com.br"]));
    }

    [Fact]
    public async Task ReceberAsync_MesmaEntregaDeNovo_RepetidaSemGravar()
    {
        await Comandos().ReceberAsync(Mensagem(), Destino);

        var desfecho = await Comandos().ReceberAsync(Mensagem(), Destino);

        desfecho.Should().Be(DesfechoEntrada.Repetida);
        _repositorio.Itens.Should().ContainSingle();
        _unidade.Gravacoes.Should().Be(1);
    }

    [Fact]
    public async Task ReceberAsync_OutroMessageId_OutroRegistro()
    {
        await Comandos().ReceberAsync(Mensagem(), Destino);

        await Comandos().ReceberAsync(Mensagem("outro@mail.exemplo.com"), Destino);

        _repositorio.Itens.Should().HaveCount(2);
    }

    [Theory] // F15-85, F15-87, F15-88: descarte registrado, como IGNORADO, sem criar nada
    [InlineData("automatico", DesfechoEntrada.Automatica)]
    [InlineData("laco", DesfechoEntrada.Laco)]
    [InlineData("filtro", DesfechoEntrada.Filtrada)]
    public async Task ReceberAsync_AutomaticoLacoOuFiltro_RegistraIgnoradoComODescarte(string caso, DesfechoEntrada esperado)
    {
        var mensagem = caso switch
        {
            "automatico" => Mensagem() with { Sinais = new SinaisDeAutomatico(AutoSubmitted: "auto-replied") },
            "laco" => Mensagem() with { Remetentes = ["nao-responda@send.acordu.app.br"] },
            _ => Mensagem() with { Filtro = "spam" },
        };

        var desfecho = await Comandos().ReceberAsync(mensagem, Destino);

        desfecho.Should().Be(esperado);
        var registro = _repositorio.Itens.Should().ContainSingle().Subject;
        registro.Resultado.Should().Be(ResultadoEntradaEmail.Ignorado);
        _unidade.Gravacoes.Should().Be(1);
    }

    [Fact]
    public async Task ReceberAsync_AutomaticoRepetido_RepetidaSemGravar() // a repetição vem antes do descarte (D6)
    {
        var automatico = Mensagem() with { Sinais = new SinaisDeAutomatico(TipoDoCorpo: "multipart/report") };
        await Comandos().ReceberAsync(automatico, Destino);

        var desfecho = await Comandos().ReceberAsync(automatico, Destino);

        desfecho.Should().Be(DesfechoEntrada.Repetida);
        _repositorio.Itens.Should().ContainSingle();
    }

    [Fact]
    public async Task ReceberAsync_SemTenantNoContexto_Recusa()
    {
        var receber = () => Comandos(new SemTenant()).ReceberAsync(Mensagem(), Destino);

        await receber.Should().ThrowAsync<InvalidOperationException>();
        _repositorio.Itens.Should().BeEmpty();
    }

    [Fact] // F15-10, F15-11, F15-13
    public async Task ReceberAsync_RemetenteConhecido_AbreSolicitacaoEmailComOContatoEARegistraSolicitacao()
    {
        var contato = ContatoConhecido();

        var desfecho = await Comandos().ReceberAsync(DoContato(), Destino);

        desfecho.Should().Be(DesfechoEntrada.Aberta);
        var solicitacao = _solicitacoes.Itens.Should().ContainSingle().Subject;
        solicitacao.TenantId.Should().Be(Empresa);
        solicitacao.Numero.Should().Be(42, "o próximo protocolo");
        solicitacao.Origem.Should().Be(OrigemSolicitacao.Email);
        solicitacao.Status.Should().Be(StatusSolicitacao.Aberta);
        solicitacao.ClienteId.Should().Be(contato.ClienteId);
        solicitacao.ContatoId.Should().Be(contato.ContatoId);
        solicitacao.Prioridade.Should().Be(Prioridade.Media);
        solicitacao.ResponsavelId.Should().BeNull();
        solicitacao.ContratoId.Should().BeNull("sem contrato vigente, sem SLA");
        solicitacao.Titulo.Should().Be("Forno parou de novo");
        solicitacao.Descricao.Should().Be("O forno parou de novo hoje cedo.");
        _interacoes.Itens.Should().BeEmpty("sem anexos, nenhuma mensagem");
        var registro = _repositorio.Itens.Should().ContainSingle().Subject;
        registro.Resultado.Should().Be(ResultadoEntradaEmail.Solicitacao);
        _unidade.Gravacoes.Should().Be(1, "a solicitação, a conversa e o registro na mesma transação");
    }

    [Fact] // design D4 e D9: a conversa nasce na abertura, com o fio do e-mail
    public async Task ReceberAsync_RemetenteConhecido_ConversaComOMessageIdEOAssuntoOriginal()
    {
        var contato = ContatoConhecido();

        await Comandos().ReceberAsync(DoContato(), Destino);

        var conversa = _conversas.Itens.Should().ContainSingle().Subject;
        conversa.SolicitacaoId.Should().Be(_solicitacoes.Itens.Single().Id);
        conversa.ClienteId.Should().Be(contato.ClienteId);
        conversa.MessageIdOrigem.Should().Be("abc@mail.exemplo.com");
        conversa.AssuntoOrigem.Should().Be("RE: Fwd: Forno parou de novo", "o assunto como o contato escreveu, para o Re: do fio");
        ConversaPorEmail.TokenValido(conversa.Token).Should().BeTrue();
    }

    [Fact] // design D4: sem Message-ID não há a que responder
    public async Task ReceberAsync_RemetenteConhecidoSemMessageId_AbreComConversaSemFio()
    {
        ContatoConhecido();

        var desfecho = await Comandos().ReceberAsync(DoContato(messageId: null), Destino);

        desfecho.Should().Be(DesfechoEntrada.Aberta);
        var conversa = _conversas.Itens.Should().ContainSingle().Subject;
        conversa.MessageIdOrigem.Should().BeNull();
        conversa.AssuntoOrigem.Should().BeNull();
    }

    [Fact] // design D4: um Message-ID maior que uma linha de cabeçalho não serve de fio; a solicitação abre do mesmo jeito
    public async Task ReceberAsync_RemetenteConhecidoComMessageIdGigante_AbreComConversaSemFio()
    {
        ContatoConhecido();

        var desfecho = await Comandos().ReceberAsync(DoContato(messageId: new string('m', 1_000) + "@mail.exemplo.com"), Destino);

        desfecho.Should().Be(DesfechoEntrada.Aberta);
        _solicitacoes.Itens.Should().ContainSingle();
        var conversa = _conversas.Itens.Should().ContainSingle().Subject;
        conversa.MessageIdOrigem.Should().BeNull();
        conversa.AssuntoOrigem.Should().BeNull();
    }

    [Fact] // design D4: o e-mail sem assunto guarda o assunto vazio (o aviso sai "Re: (#N)"), não nulo
    public async Task ReceberAsync_SemAssunto_ConversaComAssuntoOriginalVazio()
    {
        ContatoConhecido();

        await Comandos().ReceberAsync(DoContato() with { Assunto = null }, Destino);

        var conversa = _conversas.Itens.Should().ContainSingle().Subject;
        conversa.MessageIdOrigem.Should().Be("abc@mail.exemplo.com");
        conversa.AssuntoOrigem.Should().BeEmpty();
    }

    [Fact] // design §1.2: o autor é a conta do contato, ou o próprio contato sem ela
    public async Task ReceberAsync_ContatoComConta_AbreComOContatoDaSolicitacao()
    {
        var contato = ContatoConhecido(usuario: Guid.NewGuid());

        await Comandos().ReceberAsync(DoContato(), Destino);

        _solicitacoes.Itens.Single().ContatoId.Should().Be(contato.ContatoId);
    }

    [Theory] // F15-12
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("Re: Fwd:")]
    public async Task ReceberAsync_SemAssunto_TituloEmailSemAssunto(string? assunto)
    {
        ContatoConhecido();

        await Comandos().ReceberAsync(DoContato() with { Assunto = assunto }, Destino);

        _solicitacoes.Itens.Should().ContainSingle().Which.Titulo.Should().Be("E-mail sem assunto");
    }

    [Fact] // F15-13: corpo vazio deixa a descrição vazia
    public async Task ReceberAsync_CorpoVazio_SemDescricao()
    {
        ContatoConhecido();

        await Comandos().ReceberAsync(DoContato() with { Texto = null }, Destino);

        _solicitacoes.Itens.Should().ContainSingle().Which.Descricao.Should().BeNull();
    }

    [Theory] // F15-26: remetente conhecido só com autenticidade pass (D11); o contato declarado vai à triagem
    [InlineData("fail")]
    [InlineData("none")]
    [InlineData(null)]
    public async Task ReceberAsync_AutenticidadeQueNaoEPass_TriagemComoRemetenteNaoConfirmado(string? autenticidade)
    {
        ContatoConhecido();

        var desfecho = await Comandos().ReceberAsync(DoContato() with { Autenticidade = autenticidade }, Destino);

        desfecho.Should().Be(DesfechoEntrada.Triagem);
        _solicitacoes.Itens.Should().BeEmpty();
        _interacoes.Itens.Should().BeEmpty("nem mensagem");
        _conversas.Itens.Should().BeEmpty();
        _triagem.Itens.Should().ContainSingle().Which.Motivo.Should().Be(MotivoTriagem.RemetenteNaoConfirmado);
        _repositorio.Itens.Should().ContainSingle().Which.Resultado.Should().Be(ResultadoEntradaEmail.Triagem);
    }

    [Fact] // F15-24: contato em dois clientes não é remetente conhecido; a triagem guarda os clientes possíveis
    public async Task ReceberAsync_ContatoEmDoisClientes_TriagemComOsClientesPossiveis()
    {
        var um = ContatoConhecido();
        var outro = ContatoConhecido();

        var desfecho = await Comandos().ReceberAsync(DoContato(), Destino);

        desfecho.Should().Be(DesfechoEntrada.Triagem);
        _solicitacoes.Itens.Should().BeEmpty();
        var item = _triagem.Itens.Should().ContainSingle().Subject;
        item.Motivo.Should().Be(MotivoTriagem.MaisDeUmCliente);
        item.ClientesPossiveis.Should().BeEquivalentTo(new[] { um.ClienteId, outro.ClienteId });
    }

    [Theory] // F15-25
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ReceberAsync_ContatoOuClienteInativo_TriagemComoContatoInativo(bool clienteAtivo, bool contatoAtivo)
    {
        ContatoConhecido(clienteAtivo, contatoAtivo);

        var desfecho = await Comandos().ReceberAsync(DoContato(), Destino);

        desfecho.Should().Be(DesfechoEntrada.Triagem);
        _solicitacoes.Itens.Should().BeEmpty();
        var item = _triagem.Itens.Should().ContainSingle().Subject;
        item.Motivo.Should().Be(MotivoTriagem.ContatoInativo);
        item.ClientesPossiveis.Should().BeEmpty();
    }

    [Fact] // um contato inativo ao lado do ativo do mesmo e-mail não atrapalha (o e-mail é único só entre os ativos)
    public async Task ReceberAsync_ContatoAtivoEOutroInativo_AbreComOAtivo()
    {
        ContatoConhecido(contatoAtivo: false);
        var ativo = ContatoConhecido();

        await Comandos().ReceberAsync(DoContato(), Destino);

        _solicitacoes.Itens.Should().ContainSingle().Which.ContatoId.Should().Be(ativo.ContatoId);
    }

    [Fact] // design §1.2: vários endereços no From contam como desconhecido (sem um remetente só no item)
    public async Task ReceberAsync_VariosRemetentes_TriagemComoDesconhecidoSemRemetente()
    {
        ContatoConhecido();

        var desfecho = await Comandos().ReceberAsync(
            DoContato() with { Remetentes = ["ana@cliente.com.br", "beto@cliente.com.br"] }, Destino);

        desfecho.Should().Be(DesfechoEntrada.Triagem);
        _solicitacoes.Itens.Should().BeEmpty();
        var item = _triagem.Itens.Should().ContainSingle().Subject;
        item.Motivo.Should().Be(MotivoTriagem.Desconhecido);
        item.RemetenteEmail.Should().BeNull();
    }

    [Fact] // a resposta ao endereço da conversa é do F15i: até lá, só o registro
    public async Task ReceberAsync_DestinoDaConversa_SoRegistraSemAbrir()
    {
        ContatoConhecido();

        var desfecho = await Comandos().ReceberAsync(
            DoContato(), Destino with { Endereco = "k7m2qd9w+abcdefghjkmnpqrs@entrada.acordu.app.br", Token = "abcdefghjkmnpqrs" });

        desfecho.Should().Be(DesfechoEntrada.Registrada);
        _solicitacoes.Itens.Should().BeEmpty();
    }

    [Fact] // F15-85: o automático do contato conhecido não abre solicitação
    public async Task ReceberAsync_AutomaticoDeRemetenteConhecido_DescartaSemAbrir()
    {
        ContatoConhecido();

        var desfecho = await Comandos().ReceberAsync(
            DoContato() with { Sinais = new SinaisDeAutomatico(AutoSubmitted: "auto-replied") }, Destino);

        desfecho.Should().Be(DesfechoEntrada.Automatica);
        _solicitacoes.Itens.Should().BeEmpty();
        _conversas.Itens.Should().BeEmpty();
    }

    [Fact] // F15-97: a repetição da mesma entrega não abre a segunda
    public async Task ReceberAsync_MesmoEmailDoContatoDuasVezes_UmaSolicitacaoSo()
    {
        ContatoConhecido();
        await Comandos().ReceberAsync(DoContato(), Destino);

        var desfecho = await Comandos().ReceberAsync(DoContato(), Destino);

        desfecho.Should().Be(DesfechoEntrada.Repetida);
        _solicitacoes.Itens.Should().ContainSingle();
        _conversas.Itens.Should().ContainSingle();
        _unidade.Gravacoes.Should().Be(1);
    }

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
    private static readonly byte[] Pdf = "%PDF-1.7 conteudo"u8.ToArray();

    private static AnexoDeEntrada Anexo(string? nome, byte[] conteudo, string tipo = "application/octet-stream") =>
        new(nome, tipo, conteudo);

    [Fact] // F15-14: dentro da política (inclusive a imagem embutida, sem nome), a primeira mensagem EXTERNA do contato
    public async Task ReceberAsync_AnexosDentroDaPolitica_PrimeiraMensagemExternaDoContatoNoCanalEmailSemTexto()
    {
        var contato = ContatoConhecido();
        var mensagem = DoContato() with
        {
            Anexos = [Anexo("orcamento.pdf", Pdf, "application/pdf"), Anexo(null, Png, "image/png"), Anexo("foto.png", Png)],
        };

        var desfecho = await Comandos().ReceberAsync(mensagem, Destino);

        desfecho.Should().Be(DesfechoEntrada.Aberta);
        var solicitacao = _solicitacoes.Itens.Should().ContainSingle().Subject;
        var interacao = _interacoes.Itens.Should().ContainSingle("só a mensagem dos anexos: nada recusado, nada de nota").Subject;
        interacao.SolicitacaoId.Should().Be(solicitacao.Id);
        interacao.Visibilidade.Should().Be(Visibilidade.Externa);
        interacao.AutorEhCliente.Should().BeTrue();
        interacao.AutorId.Should().Be(contato.ContatoId, "sem conta do portal, o autor é o próprio contato");
        interacao.Canal.Should().Be(CanalMensagem.Email);
        interacao.Texto.Should().BeNull();
        interacao.Anexos.Select(a => (a.NomeArquivo, a.TipoConteudo)).Should().Equal(
            ("orcamento.pdf", TiposDeArquivo.Pdf), ("anexo-2.png", TiposDeArquivo.Png), ("foto.png", TiposDeArquivo.Png));
        interacao.Anexos.Should().OnlyContain(a => a.ChaveArmazenamento == $"{Empresa}/{solicitacao.Id}/{a.Id}");
        _armazenamento.Enviadas.Select(e => e.Chave).Should().Equal(interacao.Anexos.Select(a => a.ChaveArmazenamento));
        var registro = _repositorio.Itens.Should().ContainSingle().Subject;
        registro.SolicitacaoId.Should().Be(solicitacao.Id);
        registro.Recusados.Should().BeNull();
        _unidade.Gravacoes.Should().Be(1, "a solicitação, a mensagem, a conversa e o registro na mesma transação");
    }

    [Fact] // F15-15: os válidos entram, a nota do sistema lista os recusados e o registro os guarda para o aviso
    public async Task ReceberAsync_ParteForaDaPolitica_ValidosEntramNotaDoSistemaERegistroComOsRecusados()
    {
        ContatoConhecido();
        var grande = new byte[RecebimentoAnexos.Politica.TamanhoMaximoBytes + 1];
        Png.CopyTo(grande, 0);
        var mensagem = DoContato() with
        {
            Anexos =
            [
                Anexo("instalador.exe", [0x4D, 0x5A, 0x90, 0x00, 0x03]),
                Anexo("video.png", grande),
                .. Enumerable.Range(1, 6).Select(n => Anexo($"foto-{n}.png", Png)),
            ],
        };

        await Comandos().ReceberAsync(mensagem, Destino);

        var solicitacao = _solicitacoes.Itens.Should().ContainSingle("a solicitação é criada com os válidos").Subject;
        var doContato = _interacoes.Itens.Should().ContainSingle(i => i.AutorEhCliente).Subject;
        doContato.Anexos.Select(a => a.NomeArquivo).Should().Equal("foto-1.png", "foto-2.png", "foto-3.png", "foto-4.png", "foto-5.png");
        var nota = _interacoes.Itens.Should().ContainSingle(i => !i.AutorEhCliente).Subject;
        nota.SolicitacaoId.Should().Be(solicitacao.Id);
        nota.Visibilidade.Should().Be(Visibilidade.Interna);
        nota.AutorId.Should().Be(AutoresDoSistema.EntradaEmail);
        nota.Canal.Should().Be(CanalMensagem.Email);
        nota.Anexos.Should().BeEmpty();
        nota.Texto.Should().Be(
            """
            Anexos do e-mail que não entraram na solicitação:
            - instalador.exe: tipo de arquivo não permitido
            - video.png: maior que 20 MB
            - foto-6.png: além do limite de 5 anexos por e-mail
            """.ReplaceLineEndings("\n"));
        nota.Eventos.Should().BeEmpty("a nota do sistema não gera aviso, feed nem seguimento");
        var registro = _repositorio.Itens.Should().ContainSingle().Subject;
        registro.SolicitacaoId.Should().Be(solicitacao.Id);
        registro.Recusados!.Itens.Should().Equal(
            new AnexoRecusado("instalador.exe", MotivoRecusaAnexo.TipoNaoPermitido),
            new AnexoRecusado("video.png", MotivoRecusaAnexo.GrandeDemais),
            new AnexoRecusado("foto-6.png", MotivoRecusaAnexo.AlemDoLimite));
        _armazenamento.Enviadas.Should().HaveCount(5, "só os válidos sobem ao storage");
    }

    [Fact] // F15-15: todos recusados, a solicitação nasce sem a mensagem do contato, só com a nota
    public async Task ReceberAsync_TodosRecusados_SemMensagemDoContatoSoANota()
    {
        ContatoConhecido();
        var mensagem = DoContato() with { Anexos = [Anexo("conversa.eml", "From: a@b.c\r\n\r\noi"u8.ToArray(), "message/rfc822")] };

        var desfecho = await Comandos().ReceberAsync(mensagem, Destino);

        desfecho.Should().Be(DesfechoEntrada.Aberta);
        _solicitacoes.Itens.Should().ContainSingle();
        _interacoes.Itens.Should().ContainSingle().Which.AutorId.Should().Be(AutoresDoSistema.EntradaEmail);
        _armazenamento.Enviadas.Should().BeEmpty();
    }

    [Fact] // como no portal: a gravação que falha tira do storage o que já subiu
    public async Task ReceberAsync_GravacaoFalha_DescartaOsAnexosEnviados()
    {
        ContatoConhecido();
        var mensagem = DoContato() with { Anexos = [Anexo("foto.png", Png), Anexo("orcamento.pdf", Pdf)] };

        var receber = () => Comandos(unidade: new UnidadeQueFalha()).ReceberAsync(mensagem, Destino);

        await receber.Should().ThrowAsync<InvalidOperationException>();
        _armazenamento.Removidas.Should().BeEquivalentTo(_armazenamento.Enviadas.Select(e => e.Chave));
        _armazenamento.Removidas.Should().HaveCount(2);
    }

    [Fact] // F15-70: os anexos do não identificado sobem na chave da triagem, nunca na de uma solicitação
    public async Task ReceberAsync_RemetenteNaoIdentificadoComAnexos_SobemNaChaveDaTriagem()
    {
        var mensagem = DoContato() with { Anexos = [Anexo("foto.png", Png)] };

        var desfecho = await Comandos().ReceberAsync(mensagem, Destino);

        desfecho.Should().Be(DesfechoEntrada.Triagem);
        var item = _triagem.Itens.Should().ContainSingle().Subject;
        var enviada = _armazenamento.Enviadas.Should().ContainSingle().Subject;
        enviada.Chave.Should().StartWith($"{Empresa}/triagem/{item.Id}/");
        item.Anexos.Should().ContainSingle().Which.ChaveArmazenamento.Should().Be(enviada.Chave);
        _repositorio.Itens.Should().ContainSingle().Which.Recusados.Should().BeNull("os recusados da triagem ficam no item");
    }

    [Fact] // F15-97 com anexos: a repetição não sobe de novo nem repete a nota
    public async Task ReceberAsync_MesmoEmailComAnexosRecusadosDuasVezes_NadaDuplica()
    {
        ContatoConhecido();
        var mensagem = DoContato() with { Anexos = [Anexo("foto.png", Png), Anexo("programa.exe", [0x4D, 0x5A, 0x90, 0x00])] };
        await Comandos().ReceberAsync(mensagem, Destino);

        var desfecho = await Comandos().ReceberAsync(mensagem, Destino);

        desfecho.Should().Be(DesfechoEntrada.Repetida);
        _armazenamento.Enviadas.Should().ContainSingle("o anexo válido sobe uma vez só");
        _interacoes.Itens.Should().HaveCount(2, "a mensagem dos anexos e uma nota, sem repetir");
        _repositorio.Itens.Should().ContainSingle();
        _unidade.Gravacoes.Should().Be(1);
    }

    [Fact] // o descartado (automático) não sobe anexo nenhum nem cria nota
    public async Task ReceberAsync_AutomaticoComAnexos_NadaSobeNemViraNota()
    {
        ContatoConhecido();
        var mensagem = DoContato() with
        {
            Sinais = new SinaisDeAutomatico(AutoSubmitted: "auto-replied"),
            Anexos = [Anexo("foto.png", Png), Anexo("programa.exe", [0x4D, 0x5A, 0x90, 0x00])],
        };

        var desfecho = await Comandos().ReceberAsync(mensagem, Destino);

        desfecho.Should().Be(DesfechoEntrada.Automatica);
        _armazenamento.Enviadas.Should().BeEmpty();
        _interacoes.Itens.Should().BeEmpty();
        _repositorio.Itens.Should().ContainSingle().Which.Recusados.Should().BeNull();
    }

    [Fact] // falha no meio do envio ao storage: o que já subiu sai, e nada é gravado
    public async Task ReceberAsync_StorageFalhaNoMeio_DescartaOsJaEnviadosENadaGrava()
    {
        ContatoConhecido();
        var storage = new ArmazenamentoQueFalhaNoTerceiro();
        var mensagem = DoContato() with { Anexos = [Anexo("a.png", Png), Anexo("b.png", Png), Anexo("c.png", Png)] };

        var receber = () => Comandos(armazenamento: storage).ReceberAsync(mensagem, Destino);

        await receber.Should().ThrowAsync<IOException>();
        storage.Ficaram.Should().BeEmpty("nenhum objeto órfão no storage");
        _solicitacoes.Itens.Should().BeEmpty();
        _interacoes.Itens.Should().BeEmpty();
        _repositorio.Itens.Should().BeEmpty();
        _unidade.Gravacoes.Should().Be(0);
    }

    [Fact] // muitos recusados: a nota lista 20 e conta o resto; o registro guarda todos para o aviso
    public async Task ReceberAsync_MuitosRecusados_NotaComOTetoERegistroComTodos()
    {
        ContatoConhecido();
        var mensagem = DoContato() with
        {
            Anexos = [.. Enumerable.Range(1, 30).Select(n => Anexo($"{n}.exe", [0x4D, 0x5A, 0x90, 0x00]))],
        };

        await Comandos().ReceberAsync(mensagem, Destino);

        var nota = _interacoes.Itens.Should().ContainSingle().Subject;
        nota.Texto.Should().Contain("- 20.exe").And.NotContain("- 21.exe").And.EndWith("- e mais 10 arquivos");
        _repositorio.Itens.Should().ContainSingle().Which.Recusados!.Itens.Should().HaveCount(30);
    }

    [Fact] // D15 e RNF-SEG-03: a nota do sistema é INTERNA, sem anexos e sem evento, em qualquer combinação
    public async Task ReceberAsync_NotaDoSistema_InternaSemEventoDeAviso()
    {
        ContatoConhecido();
        var mensagem = DoContato() with { Anexos = [Anexo("foto.png", Png), Anexo("programa.exe", [0x4D, 0x5A, 0x90, 0x00])] };

        await Comandos().ReceberAsync(mensagem, Destino);

        var nota = _interacoes.Itens.Should().ContainSingle(i => AutoresDoSistema.Eh(i.AutorId)).Subject;
        nota.Visibilidade.Should().Be(Visibilidade.Interna);
        nota.Eventos.Should().BeEmpty();
        _interacoes.Itens.Where(i => !AutoresDoSistema.Eh(i.AutorId)).Should().OnlyContain(i => i.Visibilidade == Visibilidade.Externa);
    }

    [Fact] // F15-70: o item guarda remetente, nome, assunto, texto, a recepção e o motivo; nada é respondido nem avisado
    public async Task ReceberAsync_RemetenteDesconhecido_ItemComRemetenteAssuntoTextoEMotivoSemEvento()
    {
        var mensagem = Mensagem() with
        {
            Remetentes = ["Estranho@Outro.com.br"],
            NomeDoRemetente = "  Pedro\r\n Estranho ",
            Assunto = "Re: Orçamento\r\n do forno",
            Texto = "Quanto custa?\r\nObrigado.\0",
            Autenticidade = "pass",
        };

        var desfecho = await Comandos().ReceberAsync(mensagem, Destino);

        desfecho.Should().Be(DesfechoEntrada.Triagem);
        var item = _triagem.Itens.Should().ContainSingle().Subject;
        item.TenantId.Should().Be(Empresa);
        item.RemetenteEmail.Should().Be("estranho@outro.com.br", "normalizado como o e-mail dos contatos");
        item.RemetenteNome.Should().Be("Pedro Estranho");
        item.Assunto.Should().Be("Re: Orçamento do forno", "o assunto como veio, numa linha");
        item.Texto.Should().Be("Quanto custa?\nObrigado.", "o texto como a descrição da abertura (F15-13)");
        item.RecebidoEm.Should().Be(Falsos.Agora);
        item.Motivo.Should().Be(MotivoTriagem.Desconhecido);
        item.Anexos.Should().BeEmpty();
        item.Recusados.Should().BeNull();
        item.Eventos.Should().BeEmpty("nada é respondido ao remetente nem avisado à equipe");
        _solicitacoes.Itens.Should().BeEmpty();
        _interacoes.Itens.Should().BeEmpty();
        _conversas.Itens.Should().BeEmpty();
        _unidade.Gravacoes.Should().Be(1, "o item e o registro na mesma transação");
    }

    [Fact] // F15-20 (base): o desconhecido com a autenticidade falhando é só desconhecido
    public async Task ReceberAsync_DesconhecidoSemAutenticidade_TriagemComoDesconhecido()
    {
        var desfecho = await Comandos().ReceberAsync(Mensagem() with { Autenticidade = "fail" }, Destino);

        desfecho.Should().Be(DesfechoEntrada.Triagem);
        _triagem.Itens.Should().ContainSingle().Which.Motivo.Should().Be(MotivoTriagem.Desconhecido);
    }

    [Fact] // F15-70: e-mail sem From (nem assunto, nem corpo) também vai à triagem, sem remetente
    public async Task ReceberAsync_SemFromNemConteudo_TriagemSemRemetente()
    {
        var desfecho = await Comandos().ReceberAsync(Mensagem() with { Remetentes = [] }, Destino);

        desfecho.Should().Be(DesfechoEntrada.Triagem);
        var item = _triagem.Itens.Should().ContainSingle().Subject;
        item.RemetenteEmail.Should().BeNull();
        item.RemetenteNome.Should().BeNull();
        item.Assunto.Should().BeNull();
        item.Texto.Should().BeNull();
        _repositorio.Itens.Should().ContainSingle().Which.RemetenteHash.Should().BeNull();
    }

    [Fact] // F15-70 e D14: anexos um por um, como na abertura; os recusados ficam no item, sem nota (não há conversa)
    public async Task ReceberAsync_TriagemComAnexosForaDaPolitica_ValidosNoItemERecusadosComOMotivo()
    {
        var mensagem = Mensagem() with
        {
            Anexos =
            [
                Anexo("orcamento.pdf", Pdf, "application/pdf"),
                Anexo("programa.exe", [0x4D, 0x5A, 0x90, 0x00]),
                .. Enumerable.Range(1, 5).Select(n => Anexo($"foto-{n}.png", Png)),
            ],
        };

        await Comandos().ReceberAsync(mensagem, Destino);

        var item = _triagem.Itens.Should().ContainSingle().Subject;
        item.Anexos.Select(a => a.NomeArquivo).Should().Equal("orcamento.pdf", "foto-1.png", "foto-2.png", "foto-3.png", "foto-4.png");
        item.Anexos.Should().OnlyContain(a => a.ChaveArmazenamento.StartsWith($"{Empresa}/triagem/{item.Id}/", StringComparison.Ordinal));
        item.Recusados!.Itens.Select(r => (r.NomeArquivo, r.Motivo)).Should().Equal(
            ("programa.exe", MotivoRecusaAnexo.TipoNaoPermitido), ("foto-5.png", MotivoRecusaAnexo.AlemDoLimite));
        _armazenamento.Enviadas.Should().HaveCount(5, "só os válidos sobem");
        _interacoes.Itens.Should().BeEmpty("sem solicitação, sem nota do sistema");
    }

    [Fact] // F15-103: a repetição da mesma entrega não cria o segundo item nem sobe os anexos de novo
    public async Task ReceberAsync_MesmoEmailNaoIdentificadoDuasVezes_UmItemSo()
    {
        var mensagem = Mensagem() with { Anexos = [Anexo("foto.png", Png)] };
        await Comandos().ReceberAsync(mensagem, Destino);

        var desfecho = await Comandos().ReceberAsync(mensagem, Destino);

        desfecho.Should().Be(DesfechoEntrada.Repetida);
        _triagem.Itens.Should().ContainSingle();
        _armazenamento.Enviadas.Should().ContainSingle();
        _repositorio.Itens.Should().ContainSingle();
        _unidade.Gravacoes.Should().Be(1);
    }

    [Fact] // F15-103: a corrida de duas entregas iguais esbarra no índice do registro; os anexos da triagem saem do storage
    public async Task ReceberAsync_TriagemGravacaoFalha_DescartaOsAnexosEnviados()
    {
        var mensagem = Mensagem() with { Anexos = [Anexo("foto.png", Png), Anexo("orcamento.pdf", Pdf)] };

        var receber = () => Comandos(unidade: new UnidadeQueFalha()).ReceberAsync(mensagem, Destino);

        await receber.Should().ThrowAsync<InvalidOperationException>();
        _armazenamento.Enviadas.Should().HaveCount(2);
        _armazenamento.Removidas.Should().BeEquivalentTo(_armazenamento.Enviadas.Select(e => e.Chave));
    }

    [Theory] // F15-70 (D10): From com endereço inválido ou vazio é DESCONHECIDO, sem remetente, mesmo com contato de outro endereço
    [InlineData("sem-arroba")]
    [InlineData("")]
    [InlineData("@sem-local.com.br")]
    public async Task ReceberAsync_FromInvalidoOuVazio_TriagemComoDesconhecidoSemRemetente(string remetente)
    {
        ContatoConhecido();
        var mensagem = DoContato() with { Remetentes = [remetente] };

        var desfecho = await Comandos().ReceberAsync(mensagem, Destino);

        desfecho.Should().Be(DesfechoEntrada.Triagem);
        _solicitacoes.Itens.Should().BeEmpty();
        var item = _triagem.Itens.Should().ContainSingle().Subject;
        item.Motivo.Should().Be(MotivoTriagem.Desconhecido);
        item.RemetenteEmail.Should().BeNull();
        item.ClientesPossiveis.Should().BeEmpty();
    }

    [Fact] // F15-70: nome, assunto e texto enormes são cortados no limite do item; senão a entrada daria 5xx e o provedor reenviaria
    public async Task ReceberAsync_TriagemComCamposAlemDoLimite_CortadosNoLimiteSemFalhar()
    {
        var mensagem = Mensagem() with
        {
            NomeDoRemetente = new string('N', 300),
            Assunto = new string('A', 2_000),
            Texto = new string('T', 20_000),
        };

        var desfecho = await Comandos().ReceberAsync(mensagem, Destino);

        desfecho.Should().Be(DesfechoEntrada.Triagem);
        var item = _triagem.Itens.Should().ContainSingle().Subject;
        item.RemetenteNome.Should().HaveLength(LimitesTexto.Nome);
        item.Assunto.Should().HaveLength(ItemTriagemEmail.TamanhoAssunto);
        item.Texto.Should().HaveLength(LimitesTexto.Descricao);
    }

    [Theory] // D12: o descarte vem antes da triagem; o automático, o laço e o filtro de quem não é conhecido não criam item nem sobem anexo
    [InlineData("automatico", DesfechoEntrada.Automatica)]
    [InlineData("laco", DesfechoEntrada.Laco)]
    [InlineData("filtro", DesfechoEntrada.Filtrada)]
    public async Task ReceberAsync_DescarteDeRemetenteNaoIdentificado_NadaVaiATriagemNemSobeAnexo(string caso, DesfechoEntrada esperado)
    {
        var mensagem = caso switch
        {
            "automatico" => Mensagem() with { Sinais = new SinaisDeAutomatico(AutoSubmitted: "auto-replied") },
            "laco" => Mensagem() with { Remetentes = ["nao-responda@send.acordu.app.br"] },
            _ => Mensagem() with { Filtro = "spam" },
        };

        var desfecho = await Comandos().ReceberAsync(mensagem with { Anexos = [Anexo("foto.png", Png)] }, Destino);

        desfecho.Should().Be(esperado);
        _triagem.Itens.Should().BeEmpty();
        _armazenamento.Enviadas.Should().BeEmpty();
        _repositorio.Itens.Should().ContainSingle().Which.ItemTriagemId.Should().BeNull();
    }

    [Fact] // F15-24/F15-25: o cliente inativo de um contato com o mesmo e-mail não impede o ativo de outro cliente (um só candidato ativo)
    public async Task ReceberAsync_ContatoEmClienteInativoEEmClienteAtivo_AbreNoAtivo()
    {
        ContatoConhecido(clienteAtivo: false);
        var ativo = ContatoConhecido();

        var desfecho = await Comandos().ReceberAsync(DoContato(), Destino);

        desfecho.Should().Be(DesfechoEntrada.Aberta);
        _solicitacoes.Itens.Should().ContainSingle().Which.ClienteId.Should().Be(ativo.ClienteId);
        _triagem.Itens.Should().BeEmpty();
    }

    [Fact] // F15-24 + F15-26: dois clientes e autenticidade falhando é "não confirmado", sem clientes possíveis no item
    public async Task ReceberAsync_ContatoEmDoisClientesSemAutenticidade_NaoConfirmadoSemClientesPossiveis()
    {
        ContatoConhecido();
        ContatoConhecido();

        await Comandos().ReceberAsync(DoContato() with { Autenticidade = "fail" }, Destino);

        var item = _triagem.Itens.Should().ContainSingle().Subject;
        item.Motivo.Should().Be(MotivoTriagem.RemetenteNaoConfirmado);
        item.ClientesPossiveis.Should().BeEmpty();
    }

    /// <summary>Storage cujo terceiro envio falha; guarda o que ficou (enviado e não removido).</summary>
    private sealed class ArmazenamentoQueFalhaNoTerceiro : IArmazenamentoArquivos
    {
        private readonly HashSet<string> _objetos = [];
        private int _envios;

        public IReadOnlyCollection<string> Ficaram => _objetos;

        public Task EnviarAsync(string chave, Stream conteudo, long tamanhoBytes, string tipoConteudo, CancellationToken cancellationToken = default)
        {
            if (++_envios == 3)
                throw new IOException("storage fora do ar");
            _objetos.Add(chave);
            return Task.CompletedTask;
        }

        public Task RemoverAsync(string chave, CancellationToken cancellationToken = default)
        {
            _objetos.Remove(chave);
            return Task.CompletedTask;
        }

        public Uri UrlDeDownload(string chave, string nomeArquivo, string tipoConteudo, TimeSpan validade) => new("https://exemplo.invalid/");
    }

    /// <summary>Contexto sem empresa (escopo que ninguém definiu).</summary>
    private sealed class SemTenant : ITenantContext
    {
        public Guid? TenantId => null;

        public bool EhCliente => false;

        public Guid? ClienteId => null;

        public Guid? UsuarioId => null;

        public string? Papel => null;

        public IReadOnlySet<string> Permissoes { get; } = new HashSet<string>();

        public EscopoPortal? EscopoPortal => null;

        public Guid? AcessoPortalId => null;

        public Guid? SolicitacaoPermitida => null;
    }

    /// <summary>Os contatos por e-mail (já normalizado), como a consulta da Infrastructure devolve.</summary>
    private sealed class ContatosEmMemoria : IContatosDeEntrada
    {
        private readonly List<(string Email, ContatoDeEntrada Contato)> _contatos = [];

        public void Adicionar(string email, ContatoDeEntrada contato) => _contatos.Add((email, contato));

        public Task<IReadOnlyList<ContatoDeEntrada>> PorEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContatoDeEntrada>>([.. _contatos.Where(c => c.Email == email).Select(c => c.Contato)]);
    }

    private sealed class Protocolo(Func<int> proximo) : IGeradorProtocolo
    {
        public Task<int> ProximoNumeroSolicitacaoAsync(CancellationToken cancellationToken = default) => Task.FromResult(proximo());
    }

    /// <summary>Nenhum contrato vigente: a solicitação sai sem SLA, como no portal.</summary>
    private sealed class SemContratos : IConsultasContrato
    {
        public Task<IReadOnlyList<Guid>> VigentesDoClienteAsync(Guid clienteId, DateOnly hoje, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<ConsumoContratoDto?> ConsumoAsync(
            Guid contratoId, MesReferencia periodo, DateOnly hoje, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ResultadoPaginado<ContratoResumoDto>> ListarAsync(
            Guid? clienteId, StatusContrato? status, Paginacao paginacao, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ContratoDto?> DetalharAsync(Guid id, DateOnly hoje, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ExisteAtivoSobrepostoAsync(
            Guid clienteId, TipoContrato tipo, Periodo vigencia, Guid? ignorar, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    /// <summary>A consulta "já existe" sobre os itens do repositório em memória.</summary>
    private sealed class RegistrosEmMemoria(Repositorio<EmailRecebido> repositorio) : IEmailsRecebidos
    {
        public Task<bool> ExisteAsync(string chaveEntrada, CancellationToken cancellationToken = default) =>
            Task.FromResult(repositorio.Itens.Exists(e => e.ChaveEntrada == chaveEntrada));

        public Task<AnexosRecusados?> RecusadosDaAberturaAsync(Guid solicitacaoId, CancellationToken cancellationToken = default) =>
            Task.FromResult(repositorio.Itens.Find(e => e.SolicitacaoId == solicitacaoId)?.Recusados);
    }

    /// <summary>A gravação que falha, como a corrida de duas entregas iguais no índice do registro.</summary>
    private sealed class UnidadeQueFalha : IUnitOfWork
    {
        public Task SalvarAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("índice único");
    }
}
