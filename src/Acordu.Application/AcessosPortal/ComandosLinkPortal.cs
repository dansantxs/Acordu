using Acordu.Application.Abstractions;
using Acordu.Application.Auditoria;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Exceptions;

namespace Acordu.Application.AcessosPortal;

/// <summary>
/// Link de acesso a uma solicitação, sem senha (RF-POR-08, segurança §5.3). Um link por (solicitação, contato): emitir
/// de novo revoga o anterior.
/// </summary>
/// <remarks>
/// Exceção à regra "e-mail só pela outbox": o link precisa voltar na resposta e o token não pode ficar em claro na
/// outbox, então o e-mail sai logo depois do commit, na própria requisição. Falha no envio não desfaz a emissão: a
/// resposta diz <c>emailEnviado: false</c> e a equipe repassa o link.
/// </remarks>
public sealed class ComandosLinkPortal
{
    public const int ValidadePadraoDias = 30;
    public const int ValidadeMaximaDias = 90;

    private readonly ITenantContext _contexto;
    private readonly IRepositorio<Solicitacao> _solicitacoes;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<AcessoPortal> _acessos;
    private readonly IAcessosDoPortal _acessosDoPortal;
    private readonly ITenantAtual _tenant;
    private readonly IEnviadorEmail _email;
    private readonly EnderecosPublicos _enderecos;
    private readonly RelogioDoTenant _relogio;
    private readonly IUnitOfWork _unidade;
    private readonly IAuditoria _auditoria;
    private readonly EnvioATerceiros _envio;

    public ComandosLinkPortal(
        ITenantContext contexto,
        IRepositorio<Solicitacao> solicitacoes,
        IRepositorio<Cliente> clientes,
        IRepositorio<AcessoPortal> acessos,
        IAcessosDoPortal acessosDoPortal,
        ITenantAtual tenant,
        IEnviadorEmail email,
        EnderecosPublicos enderecos,
        RelogioDoTenant relogio,
        IUnitOfWork unidade,
        IAuditoria auditoria,
        EnvioATerceiros envio)
    {
        ArgumentNullException.ThrowIfNull(envio);
        _envio = envio;
        ArgumentNullException.ThrowIfNull(auditoria);
        _auditoria = auditoria;
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(solicitacoes);
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(acessos);
        ArgumentNullException.ThrowIfNull(acessosDoPortal);
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(enderecos);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(unidade);
        _contexto = contexto;
        _solicitacoes = solicitacoes;
        _clientes = clientes;
        _acessos = acessos;
        _acessosDoPortal = acessosDoPortal;
        _tenant = tenant;
        _email = email;
        _enderecos = enderecos;
        _relogio = relogio;
        _unidade = unidade;
    }

    /// <summary>
    /// Empresa não liberada (RF-AUT-06) → 422 <c>email-nao-confirmado</c>, mesmo sem enviar o e-mail. Empresa de
    /// demonstração com <c>enviarEmail</c> → 422 <c>demonstracao-sem-envio</c>; sem e-mail, o link é emitido (F14-89).
    /// </summary>
    public async Task<LinkEmitidoDto> EmitirAsync(Guid solicitacaoId, EmissaoLinkRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var solicitacao = await CarregarAsync(solicitacaoId, cancellationToken);
        // Na demonstração, só o link sem e-mail sai (F14-89): o que mandaria e-mail recebe 422 demonstracao-sem-envio.
        await _envio.GarantirLiberadoAsync(comEmail: dados.EnviarEmail != false, cancellationToken);
        if (solicitacao.EstaFinalizada)
            throw new AcessoPortalException("Solicitação fechada ou cancelada não recebe link de acesso.");
        var dias = dados.ValidadeDias ?? ValidadePadraoDias;
        if (dias is < 1 or > ValidadeMaximaDias)
            throw ErroDeCampo.Criar("validadeDias", $"A validade vai de 1 a {ValidadeMaximaDias} dias.");
        var cliente = await _clientes.ObterPorIdAsync(solicitacao.ClienteId, cancellationToken);
        if (cliente is null || !cliente.Ativo)
            throw new AcessoPortalException("Cliente inativo não recebe link de acesso.");
        var contato = cliente.Contatos.FirstOrDefault(c => c.Id == dados.ContatoId && c.Ativo)
            ?? throw ErroDeCampo.Criar("contatoId", "Contato não encontrado entre os ativos do cliente da solicitação.");

        var agora = _relogio.Agora;
        foreach (var anterior in await _acessosDoPortal.LinksNaoRevogadosAsync(contato.Id, cancellationToken))
        {
            if (anterior.SolicitacaoId == solicitacao.Id)
                anterior.Revogar(agora);
        }

        var (acesso, token) = AcessoPortal.Emitir(
            Guid.CreateVersion7(), solicitacao.TenantId, cliente.Id, solicitacao.Id, contato.Id, agora, TimeSpan.FromDays(dias));
        _acessos.Adicionar(acesso);
        _auditoria.Registrar(
            AcoesAuditoria.PortalLinkEmissao, "solicitacao", solicitacao.Id,
            new { acessoPortalId = acesso.Id, contatoId = contato.Id, validadeDias = dias });
        await _unidade.SalvarAsync(cancellationToken);

        var link = _enderecos.LinkPortal(token);
        var enviado = dados.EnviarEmail != false
            && await EnviarAsync(contato, solicitacao, link, acesso.ExpiraEm, cancellationToken);
        return new LinkEmitidoDto(acesso.Id, acesso.ExpiraEm, link, enviado);
    }

    /// <summary>Revoga o link (idempotente); de outra solicitação ou invisível → 404.</summary>
    public async Task RevogarAsync(Guid solicitacaoId, Guid acessoId, CancellationToken cancellationToken = default)
    {
        var solicitacao = await CarregarAsync(solicitacaoId, cancellationToken);
        var acesso = await _acessos.ObterPorIdAsync(acessoId, cancellationToken);
        if (acesso is null || acesso.SolicitacaoId != solicitacao.Id)
            throw new NaoEncontradoException("Link não encontrado.");
        acesso.Revogar(_relogio.Agora);
        _auditoria.Registrar(
            AcoesAuditoria.PortalLinkRevogacao, "solicitacao", solicitacao.Id, new { acessoPortalId = acesso.Id, acesso.ContatoId });
        await _unidade.SalvarAsync(cancellationToken);
    }

    private async Task<Solicitacao> CarregarAsync(Guid solicitacaoId, CancellationToken cancellationToken)
    {
        _contexto.Exigir(Permissoes.PortalGerenciarAcessos);
        return await _solicitacoes.ObterPorIdAsync(solicitacaoId, cancellationToken)
            ?? throw new NaoEncontradoException("Solicitação não encontrada.");
    }

    private async Task<bool> EnviarAsync(
        Contato contato, Solicitacao solicitacao, Uri link, DateTimeOffset expiraEm, CancellationToken cancellationToken)
    {
        var tenant = await _tenant.ObterAsync(cancellationToken);
        var prestador = tenant.NomePortal ?? tenant.Nome;
        var mensagem = new MensagemEmail(
            contato.Email,
            $"{prestador}: acompanhe a solicitação #{solicitacao.Numero}",
            $"""
            Olá, {contato.Nome}!

            {prestador} enviou um link para você acompanhar a solicitação #{solicitacao.Numero} — {solicitacao.Titulo}.
            Por ele você vê o andamento, responde e aprova a entrega, sem precisar de senha.

            {link}

            O link vale até {expiraEm:dd/MM/yyyy} e é pessoal: não o encaminhe.
            """,
            "link-portal",
            prestador,
            tenant.ResponderPara);
        try
        {
            await _email.EnviarAsync(mensagem, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }
}
