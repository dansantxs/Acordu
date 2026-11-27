using Acordu.Application.Abstractions;
using Acordu.Application.Anexos;
using Acordu.Application.Solicitacoes;
using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.EntradaEmail;

/// <summary>
/// O e-mail aceito para uma empresa (RF-SOL-11; design do F15, D6), no escopo com o tenant dela fixado (ADR 0016, item 3):
/// as guardas de isolamento e de só leitura valem como em qualquer gravação. Grava o registro de entrada na mesma
/// transação do efeito; a repetição da mesma entrega não grava de novo (F15-97). Antes da identificação, o automático, o
/// laço e o filtro do provedor são descartados (<see cref="DescarteDeEntrada"/>; D11, D12): ficam só no registro, como
/// <see cref="ResultadoEntradaEmail.Ignorado"/>, e nada é criado nem respondido (F15-85, F15-87, F15-88).
/// <para>
/// O e-mail novo (ao endereço da empresa, sem token) de remetente conhecido (<see cref="IdentificacaoDoRemetente"/>) abre
/// a solicitação (F15-10 a F15-13, F15-27): origem EMAIL, o cliente e o contato do remetente, prioridade MÉDIA, sem
/// responsável, com o contrato como no portal (<see cref="AberturaPeloContato"/>), e a <see cref="ConversaPorEmail"/> com o
/// <c>Message-ID</c> e o assunto do e-mail, para os avisos seguirem no fio dele (D4, D9). Os anexos passam pela política um
/// por um (<see cref="AnexosDaEntrada"/>; D14): os válidos viram a primeira mensagem EXTERNA do contato, no canal E-mail
/// (F15-14), e os recusados, uma nota do sistema (D15) e a lista guardada no registro, que o aviso de recebida lê (F15-15).
/// Os avisos saem pelos manipuladores de sempre, depois do commit (F15-16 a F15-18).
/// </para>
/// <para>
/// O remetente não identificado vai à triagem da empresa (D10; F15-20, F15-24 a F15-26, F15-70): um
/// <see cref="ItemTriagemEmail"/> com o remetente, o assunto, o texto, o motivo (<see cref="IdentificacaoDoRemetente.Classificar"/>)
/// e os anexos pela mesma política, os válidos no storage na chave da triagem; nada é respondido nem avisado. Entre um PR e
/// outro (design §3), a resposta ao endereço da conversa (F15i) fica só no registro, como
/// <see cref="ResultadoEntradaEmail.Ignorado"/>.
/// </para>
/// </summary>
public sealed class ComandosEntradaEmail
{
    private readonly ITenantContext _contexto;
    private readonly IEmailsRecebidos _registros;
    private readonly IRepositorio<EmailRecebido> _repositorio;
    private readonly IContatosDeEntrada _contatos;
    private readonly AberturaPeloContato _abertura;
    private readonly RecebimentoAnexos _anexos;
    private readonly IRepositorio<Interacao> _interacoes;
    private readonly IRepositorio<ConversaPorEmail> _conversas;
    private readonly IRepositorio<ItemTriagemEmail> _triagem;
    private readonly IUnitOfWork _unidade;
    private readonly TimeProvider _relogio;
    private readonly OpcoesEmailEntrada _opcoes;

    public ComandosEntradaEmail(
        ITenantContext contexto,
        IEmailsRecebidos registros,
        IRepositorio<EmailRecebido> repositorio,
        IContatosDeEntrada contatos,
        AberturaPeloContato abertura,
        RecebimentoAnexos anexos,
        IRepositorio<Interacao> interacoes,
        IRepositorio<ConversaPorEmail> conversas,
        IRepositorio<ItemTriagemEmail> triagem,
        IUnitOfWork unidade,
        TimeProvider relogio,
        OpcoesEmailEntrada opcoes)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(registros);
        ArgumentNullException.ThrowIfNull(repositorio);
        ArgumentNullException.ThrowIfNull(contatos);
        ArgumentNullException.ThrowIfNull(abertura);
        ArgumentNullException.ThrowIfNull(anexos);
        ArgumentNullException.ThrowIfNull(interacoes);
        ArgumentNullException.ThrowIfNull(conversas);
        ArgumentNullException.ThrowIfNull(triagem);
        ArgumentNullException.ThrowIfNull(unidade);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(opcoes);
        _contexto = contexto;
        _registros = registros;
        _repositorio = repositorio;
        _contatos = contatos;
        _abertura = abertura;
        _anexos = anexos;
        _interacoes = interacoes;
        _conversas = conversas;
        _triagem = triagem;
        _unidade = unidade;
        _relogio = relogio;
        _opcoes = opcoes;
    }

    /// <summary>
    /// Trata o e-mail que chegou a <paramref name="destino"/> na empresa do contexto. A corrida de duas entregas iguais
    /// esbarra no índice único da chave (nada da segunda fica, nem a solicitação), e quem chama trata como repetição.
    /// </summary>
    public async Task<DesfechoEntrada> ReceberAsync(
        MensagemDeEntrada mensagem, DestinoDeEntrada destino, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        ArgumentNullException.ThrowIfNull(destino);
        var tenantId = _contexto.TenantId
            ?? throw new InvalidOperationException("A entrada por e-mail roda no escopo da empresa do destino.");

        var chave = ChaveDeEntrada.Calcular(mensagem.MessageId, destino.Endereco, mensagem.HashDoConteudo);
        if (await _registros.ExisteAsync(chave, cancellationToken))
            return DesfechoEntrada.Repetida;

        // Automático, laço e filtro (D12: depois da empresa, antes do limite e da identificação): descarte registrado.
        if (DescarteDeEntrada.Avaliar(mensagem, _opcoes) is { } descarte)
        {
            return await RegistrarAsync(
                tenantId, chave, mensagem, ResultadoEntradaEmail.Ignorado, descarte, null, null, null, cancellationToken);
        }

        // A resposta ao endereço da conversa entra no F15i (design §3): até lá, só o registro.
        if (destino.Token is not null)
        {
            return await RegistrarAsync(
                tenantId, chave, mensagem, ResultadoEntradaEmail.Ignorado, DesfechoEntrada.Registrada, null, null, null,
                cancellationToken);
        }

        var identificacao = await IdentificarAsync(mensagem, cancellationToken);
        if (identificacao.Contato is not { } contato)
            return await TriarAsync(tenantId, chave, mensagem, identificacao, cancellationToken);

        // Os válidos sobem ao storage antes do commit, com a chave da solicitação, como no portal; se a gravação falhar
        // (inclusive a corrida de duas entregas iguais no índice do registro), saem de novo.
        var solicitacaoId = Guid.CreateVersion7();
        var separacao = AnexosDaEntrada.Separar(mensagem.Anexos, RecebimentoAnexos.Politica);
        var enviados = await _anexos.EnviarAsync(tenantId, solicitacaoId, separacao.Validos, cancellationToken);
        try
        {
            await AbrirAsync(tenantId, solicitacaoId, mensagem, contato, enviados, separacao.Recusados, cancellationToken);
            return await RegistrarAsync(
                tenantId, chave, mensagem, ResultadoEntradaEmail.Solicitacao, DesfechoEntrada.Aberta, solicitacaoId,
                separacao.Recusados, null, cancellationToken);
        }
        catch
        {
            await _anexos.DescartarAsync(enviados);
            throw;
        }
    }

    /// <summary>
    /// O contato do remetente conhecido: um endereço só no <c>From</c>, autenticidade confirmada (D11; o redirecionamento
    /// da caixa do prestador mantém o remetente original e passa pelo DKIM dele, F15-27) e exatamente um contato ativo de
    /// cliente ativo com aquele e-mail na empresa; senão, o motivo da triagem (D10). O envelope não conta: serve só para o
    /// automático. Os contatos são procurados mesmo com a autenticidade falhando, para o motivo dizer se o remetente
    /// declarado é de um contato (F15-26).
    /// </summary>
    private async Task<Identificacao> IdentificarAsync(MensagemDeEntrada mensagem, CancellationToken cancellationToken)
    {
        if (IdentificacaoDoRemetente.EmailDoRemetente(mensagem.Remetentes) is not { } email)
            return Identificacao.NaTriagem(MotivoTriagem.Desconhecido);

        return IdentificacaoDoRemetente.Classificar(
            IdentificacaoDoRemetente.AutenticidadeOk(mensagem.Autenticidade),
            await _contatos.PorEmailAsync(email, cancellationToken));
    }

    /// <summary>
    /// O e-mail de remetente não identificado na triagem (D10; F15-70): o item com o remetente, o assunto, o texto (como
    /// na abertura, F15-13), o motivo e os anexos pela mesma política um por um (D14), os válidos subindo ao storage na
    /// chave da triagem antes do commit e saindo de novo se a gravação falhar (inclusive a corrida de duas entregas iguais,
    /// F15-103). Nada é respondido ao remetente nem avisado (sem <i>backscatter</i>; ADR 0016, item 4): o item não registra
    /// evento.
    /// </summary>
    private async Task<DesfechoEntrada> TriarAsync(
        Guid tenantId, string chave, MensagemDeEntrada mensagem, Identificacao identificacao, CancellationToken cancellationToken)
    {
        var itemId = Guid.CreateVersion7();
        var separacao = AnexosDaEntrada.Separar(mensagem.Anexos, RecebimentoAnexos.Politica);
        var enviados = await _anexos.EnviarParaTriagemAsync(tenantId, itemId, separacao.Validos, cancellationToken);
        try
        {
            _triagem.Adicionar(ItemTriagemEmail.Receber(
                itemId,
                tenantId,
                IdentificacaoDoRemetente.EmailDoRemetente(mensagem.Remetentes),
                ConteudoDaAbertura.NomeDoRemetente(mensagem.NomeDoRemetente),
                ConteudoDaAbertura.AssuntoOriginal(mensagem.Assunto),
                ConteudoDaAbertura.Descricao(mensagem.Texto),
                identificacao.Motivo ?? MotivoTriagem.Desconhecido,
                identificacao.ClientesPossiveis,
                enviados,
                separacao.Recusados,
                RecebimentoAnexos.Politica,
                _relogio.GetUtcNow()));
            return await RegistrarAsync(
                tenantId, chave, mensagem, ResultadoEntradaEmail.Triagem, DesfechoEntrada.Triagem, null, null, itemId,
                cancellationToken);
        }
        catch
        {
            await _anexos.DescartarAsync(enviados);
            throw;
        }
    }

    private async Task AbrirAsync(
        Guid tenantId,
        Guid solicitacaoId,
        MensagemDeEntrada mensagem,
        ContatoDeEntrada contato,
        IReadOnlyList<NovoAnexo> anexos,
        AnexosRecusados? recusados,
        CancellationToken cancellationToken)
    {
        var solicitacao = await _abertura.AbrirAsync(
            new AberturaDoContato(
                solicitacaoId,
                tenantId,
                contato.ClienteId,
                contato.ContatoId,
                // Como na sessão de link: sem conta do portal, o autor é o próprio contato (design §1.2).
                contato.UsuarioId ?? contato.ContatoId,
                ConteudoDaAbertura.Titulo(mensagem.Assunto),
                ConteudoDaAbertura.Descricao(mensagem.Texto),
                Prioridade.Media,
                OrigemSolicitacao.Email,
                CanalMensagem.Email,
                anexos),
            cancellationToken);
        var agora = _relogio.GetUtcNow();

        // Os recusados, para a equipe (D15): nota INTERNA do autor do sistema, no canal da entrada; nunca no portal nem em
        // aviso (F15-98). O contato fica sabendo pelo aviso de recebida, que lê a lista do registro (F15-15).
        if (recusados is not null)
        {
            _interacoes.Adicionar(Interacao.RegistrarNotaDoSistema(
                Guid.CreateVersion7(), tenantId, contato.ClienteId, solicitacao.Id, AutoresDoSistema.EntradaEmail,
                AnexosDaEntrada.TextoDaNota(recusados, RecebimentoAnexos.Politica), CanalMensagem.Email, agora));
        }

        // O fio dos avisos (D4): o Message-ID e o assunto do e-mail que abriu. Sem Message-ID (ou com um maior que uma linha
        // de cabeçalho), não há a que responder: a conversa nasce sem o fio, e os avisos saem com os assuntos de sempre.
        _conversas.Adicionar(mensagem.MessageId is { Length: <= ConversaPorEmail.TamanhoCabecalho } messageId
            ? ConversaPorEmail.DaAberturaPorEmail(
                Guid.CreateVersion7(), tenantId, contato.ClienteId, solicitacao.Id, messageId,
                ConteudoDaAbertura.AssuntoOriginal(mensagem.Assunto), agora)
            : ConversaPorEmail.Criar(Guid.CreateVersion7(), tenantId, contato.ClienteId, solicitacao.Id, agora));
    }

    private async Task<DesfechoEntrada> RegistrarAsync(
        Guid tenantId,
        string chave,
        MensagemDeEntrada mensagem,
        ResultadoEntradaEmail resultado,
        DesfechoEntrada desfecho,
        Guid? solicitacaoId,
        AnexosRecusados? recusados,
        Guid? itemTriagemId,
        CancellationToken cancellationToken)
    {
        _repositorio.Adicionar(EmailRecebido.Registrar(
            Guid.CreateVersion7(),
            tenantId,
            chave,
            ChaveDeEntrada.DoRemetente(mensagem.Remetentes),
            resultado,
            _relogio.GetUtcNow(),
            solicitacaoId,
            recusados,
            itemTriagemId));
        await _unidade.SalvarAsync(cancellationToken);
        return desfecho;
    }
}
