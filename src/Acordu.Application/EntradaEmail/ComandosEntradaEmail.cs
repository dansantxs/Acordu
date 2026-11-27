using Acordu.Application.Abstractions;
using Acordu.Application.Solicitacoes;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

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
/// <c>Message-ID</c> e o assunto do e-mail, para os avisos seguirem no fio dele (D4, D9). Os avisos saem pelos manipuladores
/// de sempre, depois do commit (F15-16 a F15-18). Entre um PR e outro (design §3), o remetente não identificado (a
/// triagem, F15h) e a resposta ao endereço da conversa (F15i) ficam só no registro, como
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
    private readonly IRepositorio<ConversaPorEmail> _conversas;
    private readonly IUnitOfWork _unidade;
    private readonly TimeProvider _relogio;
    private readonly OpcoesEmailEntrada _opcoes;

    public ComandosEntradaEmail(
        ITenantContext contexto,
        IEmailsRecebidos registros,
        IRepositorio<EmailRecebido> repositorio,
        IContatosDeEntrada contatos,
        AberturaPeloContato abertura,
        IRepositorio<ConversaPorEmail> conversas,
        IUnitOfWork unidade,
        TimeProvider relogio,
        OpcoesEmailEntrada opcoes)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(registros);
        ArgumentNullException.ThrowIfNull(repositorio);
        ArgumentNullException.ThrowIfNull(contatos);
        ArgumentNullException.ThrowIfNull(abertura);
        ArgumentNullException.ThrowIfNull(conversas);
        ArgumentNullException.ThrowIfNull(unidade);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(opcoes);
        _contexto = contexto;
        _registros = registros;
        _repositorio = repositorio;
        _contatos = contatos;
        _abertura = abertura;
        _conversas = conversas;
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
            return await RegistrarAsync(tenantId, chave, mensagem, ResultadoEntradaEmail.Ignorado, descarte, cancellationToken);

        // A resposta ao endereço da conversa entra no F15i, e o remetente não identificado vai à triagem no F15h (design
        // §3): até lá, só o registro.
        if (destino.Token is not null || await IdentificarAsync(mensagem, cancellationToken) is not { } contato)
        {
            return await RegistrarAsync(
                tenantId, chave, mensagem, ResultadoEntradaEmail.Ignorado, DesfechoEntrada.Registrada, cancellationToken);
        }

        await AbrirAsync(tenantId, mensagem, contato, cancellationToken);
        return await RegistrarAsync(
            tenantId, chave, mensagem, ResultadoEntradaEmail.Solicitacao, DesfechoEntrada.Aberta, cancellationToken);
    }

    /// <summary>
    /// O contato do remetente conhecido: um endereço só no <c>From</c>, autenticidade confirmada (D11; o redirecionamento
    /// da caixa do prestador mantém o remetente original e passa pelo DKIM dele, F15-27) e exatamente um contato ativo de
    /// cliente ativo com aquele e-mail na empresa. O envelope não conta: serve só para o automático.
    /// </summary>
    private async Task<ContatoDeEntrada?> IdentificarAsync(MensagemDeEntrada mensagem, CancellationToken cancellationToken)
    {
        if (!IdentificacaoDoRemetente.AutenticidadeOk(mensagem.Autenticidade)
            || IdentificacaoDoRemetente.EmailDoRemetente(mensagem.Remetentes) is not { } email)
        {
            return null;
        }

        return IdentificacaoDoRemetente.Escolher(await _contatos.PorEmailAsync(email, cancellationToken));
    }

    private async Task AbrirAsync(Guid tenantId, MensagemDeEntrada mensagem, ContatoDeEntrada contato, CancellationToken cancellationToken)
    {
        var solicitacao = await _abertura.AbrirAsync(
            new AberturaDoContato(
                Guid.CreateVersion7(),
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
                []),
            cancellationToken);

        // O fio dos avisos (D4): o Message-ID e o assunto do e-mail que abriu. Sem Message-ID (ou com um maior que uma linha
        // de cabeçalho), não há a que responder: a conversa nasce sem o fio, e os avisos saem com os assuntos de sempre.
        var agora = _relogio.GetUtcNow();
        _conversas.Adicionar(mensagem.MessageId is { Length: <= ConversaPorEmail.TamanhoCabecalho } messageId
            ? ConversaPorEmail.DaAberturaPorEmail(
                Guid.CreateVersion7(), tenantId, contato.ClienteId, solicitacao.Id, messageId,
                ConteudoDaAbertura.AssuntoOriginal(mensagem.Assunto), agora)
            : ConversaPorEmail.Criar(Guid.CreateVersion7(), tenantId, contato.ClienteId, solicitacao.Id, agora));
    }

    private async Task<DesfechoEntrada> RegistrarAsync(
        Guid tenantId, string chave, MensagemDeEntrada mensagem, ResultadoEntradaEmail resultado, DesfechoEntrada desfecho,
        CancellationToken cancellationToken)
    {
        _repositorio.Adicionar(EmailRecebido.Registrar(
            Guid.CreateVersion7(),
            tenantId,
            chave,
            ChaveDeEntrada.DoRemetente(mensagem.Remetentes),
            resultado,
            _relogio.GetUtcNow()));
        await _unidade.SalvarAsync(cancellationToken);
        return desfecho;
    }
}
