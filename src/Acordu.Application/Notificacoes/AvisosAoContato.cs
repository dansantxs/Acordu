using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Application.AcessosPortal;
using Acordu.Application.Anexos;
using Acordu.Application.Common;
using Acordu.Application.EntradaEmail;
using Acordu.Application.Notificacoes.Canais;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;

namespace Acordu.Application.Notificacoes;

/// <summary>
/// E-mails ao contato da solicitação (RF-NOT-01), depois do commit: recebida (com o protocolo), mensagem externa da
/// equipe, aguardando você e resolvida (com o pedido de aprovação). Sem contato, ou com contato/cliente inativo, nada
/// sai; o que o próprio contato fez (a resposta dele) não gera e-mail para ele. Empresa não liberada (RF-AUT-06): o
/// aviso é descartado, sem gerar link.
/// <para>
/// O aviso sai pelo canal da origem (RF-NOT-06; design do F15, D7): este manipulador decide o quê e para quem, monta o
/// conteúdo e o entrega ao <see cref="SeletorDeCanal"/>. Com o recebimento por e-mail ligado, o aviso leva o endereço da
/// conversa da solicitação (D3), e a <see cref="ConversaPorEmail"/> nasce no primeiro aviso, como o link sem senha. Na
/// origem EMAIL, a conversa nasce na abertura, com o fio do e-mail que a abriu, e todo aviso segue nele (D4; F15-16,
/// F15-50), com o recebimento ligado ou não: o Reply-To e a marcadora é que seguem o recebimento (F15-52). O aviso de
/// recebida da origem EMAIL diz quais anexos não entraram, lidos do registro de entrada (F15-15); a nota do sistema que os
/// lista para a equipe é INTERNA e não registra evento, então nunca vira aviso (F15-98).
/// </para>
/// </summary>
/// <remarks>
/// Link do e-mail (decidido em 13/10): contato com conta recebe o link da solicitação no portal; sem conta, um link de
/// acesso sem senha novo a cada e-mail (30 dias). Um link por (solicitação, contato): o novo revoga o anterior, então
/// o do e-mail mais recente sempre funciona. Solicitação já fechada ou cancelada não ganha link novo.
/// </remarks>
public sealed class AvisosAoContato :
    IManipuladorPosCommit<SolicitacaoAberta>,
    IManipuladorPosCommit<InteracaoRegistrada>,
    IManipuladorPosCommit<SolicitacaoStatusAlterado>,
    IManipuladorPosCommit<SolicitacaoResolvida>
{
    private readonly IRepositorio<Solicitacao> _solicitacoes;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<Interacao> _interacoes;
    private readonly IRepositorio<AcessoPortal> _acessos;
    private readonly IAcessosDoPortal _acessosDoPortal;
    private readonly IVinculos _vinculos;
    private readonly IRepositorio<ConversaPorEmail> _novasConversas;
    private readonly IConversasPorEmail _conversas;
    private readonly IEmailsRecebidos _registros;
    private readonly ITenantAtual _tenant;
    private readonly SeletorDeCanal _canais;
    private readonly OpcoesEmailEntrada _entrada;
    private readonly EnderecosPublicos _enderecos;
    private readonly RelogioDoTenant _relogio;
    private readonly IUnitOfWork _unidade;
    private readonly EnvioATerceiros _envio;

    public AvisosAoContato(
        IRepositorio<Solicitacao> solicitacoes,
        IRepositorio<Cliente> clientes,
        IRepositorio<Interacao> interacoes,
        IRepositorio<AcessoPortal> acessos,
        IAcessosDoPortal acessosDoPortal,
        IVinculos vinculos,
        IRepositorio<ConversaPorEmail> novasConversas,
        IConversasPorEmail conversas,
        IEmailsRecebidos registros,
        ITenantAtual tenant,
        SeletorDeCanal canais,
        OpcoesEmailEntrada entrada,
        EnderecosPublicos enderecos,
        RelogioDoTenant relogio,
        IUnitOfWork unidade,
        EnvioATerceiros envio)
    {
        ArgumentNullException.ThrowIfNull(solicitacoes);
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(interacoes);
        ArgumentNullException.ThrowIfNull(acessos);
        ArgumentNullException.ThrowIfNull(acessosDoPortal);
        ArgumentNullException.ThrowIfNull(vinculos);
        ArgumentNullException.ThrowIfNull(novasConversas);
        ArgumentNullException.ThrowIfNull(conversas);
        ArgumentNullException.ThrowIfNull(registros);
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(canais);
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentNullException.ThrowIfNull(enderecos);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(unidade);
        ArgumentNullException.ThrowIfNull(envio);
        _solicitacoes = solicitacoes;
        _clientes = clientes;
        _interacoes = interacoes;
        _acessos = acessos;
        _acessosDoPortal = acessosDoPortal;
        _vinculos = vinculos;
        _novasConversas = novasConversas;
        _conversas = conversas;
        _registros = registros;
        _tenant = tenant;
        _canais = canais;
        _entrada = entrada;
        _enderecos = enderecos;
        _relogio = relogio;
        _unidade = unidade;
        _envio = envio;
    }

    public async Task TratarAsync(SolicitacaoAberta evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        // Aberta por e-mail com anexos fora da política (F15-15): o contato fica sabendo quais não entraram.
        var recusados = evento.Origem == OrigemSolicitacao.Email
            ? await _registros.RecusadosDaAberturaAsync(evento.SolicitacaoId, cancellationToken)
            : null;
        var naoEntraram = recusados is null
            ? string.Empty
            : $"\n{AnexosDaEntrada.TextoDoAviso(recusados, RecebimentoAnexos.Politica)}\n";
        await AvisarAsync(evento.SolicitacaoId, "aviso-recebida", a => (
            $"Recebemos sua solicitação #{a.Solicitacao.Numero}",
            $"""
            Olá, {a.Contato.Nome}!

            {a.Prestador} recebeu sua solicitação #{a.Solicitacao.Numero} — {a.Solicitacao.Titulo}.
            Guarde este número de protocolo para acompanhar o atendimento.
            {naoEntraram}{Link(a, "Acompanhe por aqui:")}
            """), cancellationToken);
    }

    public async Task TratarAsync(InteracaoRegistrada evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        // Nota INTERNA nunca vira aviso nem aparece em e-mail ao contato (F15-53, RNF-SEG-03); a mensagem do próprio
        // contato não gera aviso para ele.
        if (evento.Visibilidade != Visibilidade.Externa || evento.AutorEhCliente)
            return;
        var interacao = await _interacoes.ObterPorIdAsync(evento.InteracaoId, cancellationToken);
        if (interacao is null || interacao.Visibilidade != Visibilidade.Externa)
            return;

        var mensagem = string.IsNullOrWhiteSpace(interacao.Texto) ? "(mensagem com anexos)" : interacao.Texto;
        // "Enviar e aguardar" (F11-04): o mesmo e-mail pede o retorno. Decide pela ação, não pelo status de agora.
        await AvisarAsync(evento.SolicitacaoId, "aviso-mensagem", a => evento.PedeRetorno
            ? (
                $"Precisamos de você na solicitação #{a.Solicitacao.Numero}",
                $"""
                Olá, {a.Contato.Nome}!

                {a.Prestador} escreveu na solicitação #{a.Solicitacao.Numero} — {a.Solicitacao.Titulo}, e aguarda a sua resposta para continuar:

                {mensagem}
                {Link(a, "Responda por aqui:")}
                """)
            : (
                $"Nova mensagem na solicitação #{a.Solicitacao.Numero}",
                $"""
                Olá, {a.Contato.Nome}!

                {a.Prestador} escreveu na solicitação #{a.Solicitacao.Numero} — {a.Solicitacao.Titulo}:

                {mensagem}
                {Link(a, "Para responder, acesse:")}
                """), cancellationToken);
    }

    public Task TratarAsync(SolicitacaoStatusAlterado evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        // Com AvisoNaMensagem, o e-mail da própria resposta já pede o retorno (F11-04): não sai outro.
        if (evento.Para != StatusSolicitacao.AguardandoCliente || evento.AvisoNaMensagem)
            return Task.CompletedTask;

        return AvisarAsync(evento.SolicitacaoId, "aviso-aguardando", a => (
            $"Precisamos de você na solicitação #{a.Solicitacao.Numero}",
            $"""
            Olá, {a.Contato.Nome}!

            A solicitação #{a.Solicitacao.Numero} — {a.Solicitacao.Titulo} está aguardando uma resposta sua para continuar.
            {Link(a, "Responda por aqui:")}
            """), cancellationToken);
    }

    public Task TratarAsync(SolicitacaoResolvida evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return AvisarAsync(evento.SolicitacaoId, "aviso-resolvida", a => (
            $"Solicitação #{a.Solicitacao.Numero} resolvida: confira e aprove",
            $"""
            Olá, {a.Contato.Nome}!

            {a.Prestador} concluiu a solicitação #{a.Solicitacao.Numero} — {a.Solicitacao.Titulo}.
            Confira a entrega e aprove ou, se faltou algo, reprove contando o motivo.
            {Link(a, "Para aprovar ou reprovar:")}
            Sem resposta em {a.DiasFechamento} dias, a solicitação é fechada automaticamente.
            """), cancellationToken);
    }

    private static string Link(Aviso aviso, string chamada) =>
        aviso.Link is { } link ? $"\n{chamada}\n{link}\n" : string.Empty;

    private async Task AvisarAsync(
        Guid solicitacaoId, string categoria, Func<Aviso, (string Assunto, string Texto)> montar, CancellationToken cancellationToken)
    {
        var solicitacao = await _solicitacoes.ObterPorIdAsync(solicitacaoId, cancellationToken);
        if (solicitacao?.ContatoId is not { } contatoId)
            return;
        var cliente = await _clientes.ObterPorIdAsync(solicitacao.ClienteId, cancellationToken);
        var contato = cliente?.Contatos.FirstOrDefault(c => c.Id == contatoId);
        if (cliente is null || contato is null || !cliente.Ativo || !contato.Ativo)
            return;
        // Cliente dos dados de exemplo (F14r, RF-EMP-05): o contato é fictício (e-mail .invalid), nenhum e-mail sai.
        if (cliente.Exemplo)
            return;
        // Empresa não liberada (RF-AUT-06, F9b): nem e-mail nem link de acesso; a operação que causou o aviso segue.
        if (!await _envio.PodeAvisarAsync(categoria, null, cancellationToken))
            return;

        var tenant = await _tenant.ObterAsync(cancellationToken);
        var prestador = tenant.NomePortal ?? tenant.Nome;
        var link = await LinkAsync(solicitacao, cliente, contato, cancellationToken);
        var conversa = await ConversaAsync(tenant, solicitacao, cancellationToken);
        var endereco = conversa is not null && _entrada.AvisosPelaConversa(tenant)
            ? _entrada.EnderecoDaConversa(tenant, conversa)
            : null;
        var fio = solicitacao.Origem == OrigemSolicitacao.Email ? conversa : null;
        var (assunto, texto) = montar(new Aviso(solicitacao, contato, prestador, link, tenant.DiasFechamentoAutomatico));
        await _canais.EnviarAsync(
            new AvisoAoContato(
                categoria, contato.Nome, contato.Email, prestador, assunto, texto, solicitacao.Id, solicitacao.Numero,
                solicitacao.Origem, tenant.ResponderPara, endereco, fio?.MessageIdOrigem, fio?.AssuntoOrigem),
            cancellationToken);
    }

    /// <summary>
    /// Com o recebimento por e-mail ligado, a conversa da solicitação (o endereço dela vai no Reply-To, F15-51), criada no
    /// primeiro aviso (design do F15, D9); desligado, nada é criado (F15-52), e só a origem EMAIL lê a conversa que já
    /// existe, pelo fio (D4). Dois avisos da mesma solicitação ao mesmo tempo esbarram no índice único da conversa: o
    /// segundo falha antes de enviar, e a outbox o repete, já com a conversa.
    /// </summary>
    private async Task<ConversaPorEmail?> ConversaAsync(Tenant tenant, Solicitacao solicitacao, CancellationToken cancellationToken)
    {
        var ligado = _entrada.AvisosPelaConversa(tenant);
        if (!ligado && solicitacao.Origem != OrigemSolicitacao.Email)
            return null;

        var conversa = await _conversas.DaSolicitacaoAsync(solicitacao.Id, cancellationToken);
        if (conversa is null && ligado)
        {
            conversa = ConversaPorEmail.Criar(
                Guid.CreateVersion7(), solicitacao.TenantId, solicitacao.ClienteId, solicitacao.Id, _relogio.Agora);
            _novasConversas.Adicionar(conversa);
            await _unidade.SalvarAsync(cancellationToken);
        }

        return conversa;
    }

    private async Task<Uri?> LinkAsync(Solicitacao solicitacao, Cliente cliente, Contato contato, CancellationToken cancellationToken)
    {
        if (contato.UsuarioId is { } usuarioId
            && await _vinculos.DoUsuarioAsync(usuarioId, cancellationToken) is { } vinculo
            && vinculo.ClienteId == cliente.Id)
        {
            return _enderecos.SolicitacaoNoPortal(solicitacao.Id);
        }

        if (solicitacao.EstaFinalizada)
            return null;

        var agora = _relogio.Agora;
        foreach (var anterior in await _acessosDoPortal.LinksNaoRevogadosAsync(contato.Id, cancellationToken))
        {
            if (anterior.SolicitacaoId == solicitacao.Id)
                anterior.Revogar(agora);
        }

        var (acesso, token) = AcessoPortal.Emitir(
            Guid.CreateVersion7(), solicitacao.TenantId, cliente.Id, solicitacao.Id, contato.Id, agora,
            TimeSpan.FromDays(ComandosLinkPortal.ValidadePadraoDias));
        _acessos.Adicionar(acesso);
        await _unidade.SalvarAsync(cancellationToken);
        return _enderecos.LinkPortal(token);
    }

    private sealed record Aviso(Solicitacao Solicitacao, Contato Contato, string Prestador, Uri? Link, int DiasFechamento);
}
