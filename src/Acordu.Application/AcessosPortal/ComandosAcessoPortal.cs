using Acordu.Application.Abstractions;
using Acordu.Application.Auditoria;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Exceptions;

namespace Acordu.Application.AcessosPortal;

/// <summary>
/// Convite, escopo e revogação do acesso de contatos ao portal (contrato §4.2, segurança §3–4). O e-mail do convite sai
/// depois do commit (<see cref="EnvioConvitePortal"/>); o aceite é público e fica no serviço de autenticação.
/// </summary>
public sealed class ComandosAcessoPortal
{
    private readonly ITenantContext _contexto;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<ConvitePortal> _convites;
    private readonly IAcessosDoPortal _acessos;
    private readonly IVinculos _vinculos;
    private readonly RevogacaoAcessoPortal _revogacao;
    private readonly RelogioDoTenant _relogio;
    private readonly IUnitOfWork _unidade;
    private readonly IAuditoria _auditoria;

    public ComandosAcessoPortal(
        ITenantContext contexto,
        IRepositorio<Cliente> clientes,
        IRepositorio<ConvitePortal> convites,
        IAcessosDoPortal acessos,
        IVinculos vinculos,
        RevogacaoAcessoPortal revogacao,
        RelogioDoTenant relogio,
        IUnitOfWork unidade,
        IAuditoria auditoria)
    {
        ArgumentNullException.ThrowIfNull(auditoria);
        _auditoria = auditoria;
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(convites);
        ArgumentNullException.ThrowIfNull(acessos);
        ArgumentNullException.ThrowIfNull(vinculos);
        ArgumentNullException.ThrowIfNull(revogacao);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(unidade);
        _contexto = contexto;
        _clientes = clientes;
        _convites = convites;
        _acessos = acessos;
        _vinculos = vinculos;
        _revogacao = revogacao;
        _relogio = relogio;
        _unidade = unidade;
    }

    /// <summary>
    /// Convida (ou reconvida: o convite pendente anterior é revogado). Contato com acesso, inativo, de cliente inativo
    /// ou com e-mail que já tem vínculo neste tenant → 422.
    /// </summary>
    public async Task ConvidarAsync(Guid clienteId, Guid contatoId, AcessoPortalRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var escopo = dados.EscopoPortal ?? throw ErroDeCampo.Criar("escopoPortal", "Informe o escopo do portal.");
        var (cliente, contato) = await CarregarAsync(clienteId, contatoId, cancellationToken);
        if (!cliente.Ativo)
            throw new AcessoPortalException("Cliente inativo não recebe convite para o portal.");
        if (!contato.Ativo)
            throw new AcessoPortalException("Contato inativo não recebe convite para o portal.");
        if (contato.UsuarioId is not null)
            throw new AcessoPortalException("O contato já tem acesso ao portal. Altere o escopo ou revogue o acesso.");
        if (await _vinculos.EmailJaVinculadoAsync(contato.Email, cancellationToken))
            throw new AcessoPortalException("Este e-mail já tem acesso a este prestador, pela equipe ou por outro cliente.");

        var agora = _relogio.Agora;
        foreach (var anterior in await _acessos.ConvitesPendentesAsync(contato.Id, cancellationToken))
            anterior.Revogar(agora);
        _convites.Adicionar(ConvitePortal.Criar(Guid.CreateVersion7(), cliente.TenantId, cliente.Id, contato.Id, escopo, agora));
        _auditoria.Registrar(AcoesAuditoria.PortalAcessoConcessao, "contato", contato.Id, new { clienteId = cliente.Id, escopo });
        await _unidade.SalvarAsync(cancellationToken);
    }

    /// <summary>
    /// Altera o escopo do acesso (vale no próximo refresh do contato) ou do convite pendente. Sem nenhum dos dois → 422.
    /// </summary>
    public async Task AlterarEscopoAsync(Guid clienteId, Guid contatoId, AcessoPortalRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var escopo = dados.EscopoPortal ?? throw ErroDeCampo.Criar("escopoPortal", "Informe o escopo do portal.");
        var (cliente, contato) = await CarregarAsync(clienteId, contatoId, cancellationToken);

        var vinculo = contato.UsuarioId is { } usuarioId ? await _vinculos.DoUsuarioAsync(usuarioId, cancellationToken) : null;
        if (vinculo is not null && vinculo.ClienteId == cliente.Id)
        {
            vinculo.AlterarEscopo(escopo);
        }
        else
        {
            var pendentes = await _acessos.ConvitesPendentesAsync(contato.Id, cancellationToken);
            if (pendentes.Count == 0)
                throw new AcessoPortalException("O contato não tem acesso ao portal nem convite pendente.");
            foreach (var convite in pendentes)
                convite.AlterarEscopo(escopo);
        }

        _auditoria.Registrar(AcoesAuditoria.PortalAcessoEscopo, "contato", contato.Id, new { clienteId = cliente.Id, escopo });
        await _unidade.SalvarAsync(cancellationToken);
    }

    /// <summary>Revoga acesso, convites e links do contato. Sem nada a revogar, não faz nada (idempotente).</summary>
    public async Task RevogarAsync(Guid clienteId, Guid contatoId, CancellationToken cancellationToken = default)
    {
        var (cliente, contato) = await CarregarAsync(clienteId, contatoId, cancellationToken);
        await _revogacao.RevogarAsync(cliente, contato.Id, cancellationToken);
        _auditoria.Registrar(AcoesAuditoria.PortalAcessoRevogacao, "contato", contato.Id, new { clienteId = cliente.Id });
        await _unidade.SalvarAsync(cancellationToken);
    }

    private async Task<(Cliente Cliente, Contato Contato)> CarregarAsync(Guid clienteId, Guid contatoId, CancellationToken cancellationToken)
    {
        _contexto.Exigir(Permissoes.PortalGerenciarAcessos);
        var cliente = await _clientes.ObterPorIdAsync(clienteId, cancellationToken)
            ?? throw new NaoEncontradoException("Cliente não encontrado.");
        var contato = cliente.Contatos.FirstOrDefault(c => c.Id == contatoId)
            ?? throw new NaoEncontradoException("Contato não encontrado.");
        return (cliente, contato);
    }
}
