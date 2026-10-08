using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;

namespace Acordu.Application.Equipe;

/// <summary>
/// Gestão da equipe (contrato §3, segurança §3.3): convidar, revogar convite, trocar papel e remover. Guarda: o tenant
/// nunca fica sem Dono (o papel padrão; papel customizado não conta). O aceite do convite é público e fica no serviço
/// de autenticação.
/// </summary>
public sealed class ComandosEquipe
{
    private readonly ITenantContext _contexto;
    private readonly IRepositorio<Papel> _papeis;
    private readonly IRepositorio<Vinculo> _repositorioVinculos;
    private readonly IRepositorio<ConviteEquipe> _convites;
    private readonly IConvitesEquipe _pendentes;
    private readonly IVinculos _vinculos;
    private readonly RelogioDoTenant _relogio;
    private readonly IUnitOfWork _unidade;

    public ComandosEquipe(
        ITenantContext contexto,
        IRepositorio<Papel> papeis,
        IRepositorio<Vinculo> repositorioVinculos,
        IRepositorio<ConviteEquipe> convites,
        IConvitesEquipe pendentes,
        IVinculos vinculos,
        RelogioDoTenant relogio,
        IUnitOfWork unidade)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(papeis);
        ArgumentNullException.ThrowIfNull(repositorioVinculos);
        ArgumentNullException.ThrowIfNull(convites);
        ArgumentNullException.ThrowIfNull(pendentes);
        ArgumentNullException.ThrowIfNull(vinculos);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(unidade);
        _contexto = contexto;
        _papeis = papeis;
        _repositorioVinculos = repositorioVinculos;
        _convites = convites;
        _pendentes = pendentes;
        _vinculos = vinculos;
        _relogio = relogio;
        _unidade = unidade;
    }

    /// <summary>
    /// Convida com um papel INTERNO (reconvidar revoga o pendente anterior). Papel de cliente ou e-mail que já tem
    /// vínculo neste tenant → 422.
    /// </summary>
    public async Task<Guid> ConvidarAsync(ConviteEquipeRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.UsuarioConvidar);
        var papel = await PapelAsync(dados.PapelId, cancellationToken);
        var agora = _relogio.Agora;
        var convite = ConviteEquipe.Criar(Guid.CreateVersion7(), dados.Email, papel, _contexto.Usuario(), agora);
        if (await _vinculos.EmailJaVinculadoAsync(convite.Email, cancellationToken))
            throw new EquipeException("Este e-mail já tem acesso a este prestador, pela equipe ou como contato de um cliente.");

        foreach (var anterior in await _pendentes.PendentesAsync(convite.Email, cancellationToken))
            anterior.Revogar(agora);
        _convites.Adicionar(convite);
        await _unidade.SalvarAsync(cancellationToken);
        return convite.Id;
    }

    /// <summary>Revoga o convite (idempotente para já revogado ou aceito).</summary>
    public async Task RevogarConviteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _contexto.Exigir(Permissoes.UsuarioConvidar);
        var convite = await _convites.ObterPorIdAsync(id, cancellationToken)
            ?? throw new NaoEncontradoException("Convite não encontrado.");
        convite.Revogar(_relogio.Agora);
        await _unidade.SalvarAsync(cancellationToken);
    }

    /// <summary>Troca o papel (vale no próximo refresh da pessoa). Papel de cliente ou rebaixar o último Dono → 422.</summary>
    public async Task TrocarPapelAsync(Guid usuarioId, TrocaPapelRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.PapelGerenciar);
        var vinculo = await DaEquipeAsync(usuarioId, cancellationToken);
        var novo = await PapelAsync(dados.PapelId, cancellationToken);
        if (novo.Tipo != TipoPapel.Interno)
            throw new EquipeException("Papel de cliente não vale para a equipe: contatos entram pelo portal.");
        if (!novo.EhDono)
            await GarantirOutroDonoAsync(vinculo, cancellationToken);

        vinculo.TrocarPapel(novo);
        await _unidade.SalvarAsync(cancellationToken);
    }

    /// <summary>Remove a pessoa do tenant e derruba as sessões dela aqui. Último Dono → 422.</summary>
    public async Task RemoverAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        _contexto.Exigir(Permissoes.UsuarioRemover);
        var vinculo = await DaEquipeAsync(usuarioId, cancellationToken);
        await GarantirOutroDonoAsync(vinculo, cancellationToken);

        _repositorioVinculos.Remover(vinculo);
        await _vinculos.RevogarSessoesAsync(usuarioId, _relogio.Agora, cancellationToken);
        await _unidade.SalvarAsync(cancellationToken);
    }

    /// <summary>Se o vínculo é de Dono, precisa existir outro Dono no tenant.</summary>
    private async Task GarantirOutroDonoAsync(Vinculo vinculo, CancellationToken cancellationToken)
    {
        var atual = await _papeis.ObterPorIdAsync(vinculo.PapelId, cancellationToken);
        if (atual is { EhDono: true } && await _vinculos.ContarDonosAsync(cancellationToken) <= 1)
            throw new EquipeException("O tenant precisa de ao menos um Dono. Promova outra pessoa antes.");
    }

    private async Task<Vinculo> DaEquipeAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var vinculo = await _vinculos.DoUsuarioAsync(usuarioId, cancellationToken);
        return vinculo is { ClienteId: null } ? vinculo : throw new NaoEncontradoException("Pessoa não encontrada na equipe.");
    }

    private async Task<Papel> PapelAsync(Guid? papelId, CancellationToken cancellationToken)
    {
        if (papelId is not { } id)
            throw ErroDeCampo.Criar("papelId", "Escolha o papel.");
        return await _papeis.ObterPorIdAsync(id, cancellationToken)
            ?? throw ErroDeCampo.Criar("papelId", "Papel não encontrado.");
    }
}
