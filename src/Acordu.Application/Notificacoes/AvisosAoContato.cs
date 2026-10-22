using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Application.AcessosPortal;
using Acordu.Application.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;

namespace Acordu.Application.Notificacoes;

/// <summary>
/// E-mails ao contato da solicitação (RF-NOT-01), depois do commit: recebida (com o protocolo), mensagem externa da
/// equipe, aguardando você e resolvida (com o pedido de aprovação). Sem contato, ou com contato/cliente inativo, nada
/// sai; o que o próprio contato fez (a resposta dele) não gera e-mail para ele.
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
    private readonly ITenantAtual _tenant;
    private readonly IEnviadorEmail _email;
    private readonly EnderecosPublicos _enderecos;
    private readonly RelogioDoTenant _relogio;
    private readonly IUnitOfWork _unidade;

    public AvisosAoContato(
        IRepositorio<Solicitacao> solicitacoes,
        IRepositorio<Cliente> clientes,
        IRepositorio<Interacao> interacoes,
        IRepositorio<AcessoPortal> acessos,
        IAcessosDoPortal acessosDoPortal,
        IVinculos vinculos,
        ITenantAtual tenant,
        IEnviadorEmail email,
        EnderecosPublicos enderecos,
        RelogioDoTenant relogio,
        IUnitOfWork unidade)
    {
        ArgumentNullException.ThrowIfNull(solicitacoes);
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(interacoes);
        ArgumentNullException.ThrowIfNull(acessos);
        ArgumentNullException.ThrowIfNull(acessosDoPortal);
        ArgumentNullException.ThrowIfNull(vinculos);
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(enderecos);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(unidade);
        _solicitacoes = solicitacoes;
        _clientes = clientes;
        _interacoes = interacoes;
        _acessos = acessos;
        _acessosDoPortal = acessosDoPortal;
        _vinculos = vinculos;
        _tenant = tenant;
        _email = email;
        _enderecos = enderecos;
        _relogio = relogio;
        _unidade = unidade;
    }

    public Task TratarAsync(SolicitacaoAberta evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return AvisarAsync(evento.SolicitacaoId, "aviso-recebida", a => (
            $"Recebemos sua solicitação #{a.Solicitacao.Numero}",
            $"""
            Olá, {a.Contato.Nome}!

            {a.Prestador} recebeu sua solicitação #{a.Solicitacao.Numero} — {a.Solicitacao.Titulo}.
            Guarde este número de protocolo para acompanhar o atendimento.
            {Link(a, "Acompanhe por aqui:")}
            """), cancellationToken);
    }

    public async Task TratarAsync(InteracaoRegistrada evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        if (evento.Visibilidade != Visibilidade.Externa || evento.AutorEhCliente)
            return;
        var interacao = await _interacoes.ObterPorIdAsync(evento.InteracaoId, cancellationToken);
        if (interacao is null)
            return;

        var mensagem = string.IsNullOrWhiteSpace(interacao.Texto) ? "(mensagem com anexos)" : interacao.Texto;
        await AvisarAsync(evento.SolicitacaoId, "aviso-mensagem", a => (
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
        if (evento.Para != StatusSolicitacao.AguardandoCliente)
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

        var tenant = await _tenant.ObterAsync(cancellationToken);
        var prestador = tenant.NomePortal ?? tenant.Nome;
        var link = await LinkAsync(solicitacao, cliente, contato, cancellationToken);
        var (assunto, texto) = montar(new Aviso(solicitacao, contato, prestador, link, tenant.DiasFechamentoAutomatico));
        await _email.EnviarAsync(new MensagemEmail(contato.Email, assunto, texto, categoria, prestador, tenant.ResponderPara), cancellationToken);
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
