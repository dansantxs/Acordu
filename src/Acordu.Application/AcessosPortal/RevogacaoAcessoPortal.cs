using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Entities;

namespace Acordu.Application.AcessosPortal;

/// <summary>
/// Revogação do acesso de um contato ao portal (segurança §3.3): convites pendentes, links por solicitação, o
/// <see cref="Vinculo"/> e os refresh tokens no tenant. Usada ao revogar, ao inativar o contato e ao inativar o cliente.
/// Não grava: quem chama salva na mesma unidade de trabalho.
/// </summary>
public sealed class RevogacaoAcessoPortal
{
    private readonly IAcessosDoPortal _acessos;
    private readonly IVinculos _vinculos;
    private readonly IRepositorio<Vinculo> _repositorioVinculos;
    private readonly RelogioDoTenant _relogio;

    public RevogacaoAcessoPortal(
        IAcessosDoPortal acessos, IVinculos vinculos, IRepositorio<Vinculo> repositorioVinculos, RelogioDoTenant relogio)
    {
        ArgumentNullException.ThrowIfNull(acessos);
        ArgumentNullException.ThrowIfNull(vinculos);
        ArgumentNullException.ThrowIfNull(repositorioVinculos);
        ArgumentNullException.ThrowIfNull(relogio);
        _acessos = acessos;
        _vinculos = vinculos;
        _repositorioVinculos = repositorioVinculos;
        _relogio = relogio;
    }

    public async Task RevogarAsync(Cliente cliente, Guid contatoId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        var contato = cliente.Contatos.FirstOrDefault(c => c.Id == contatoId)
            ?? throw new NaoEncontradoException("Contato não encontrado.");
        var agora = _relogio.Agora;

        foreach (var convite in await _acessos.ConvitesPendentesAsync(contato.Id, cancellationToken))
            convite.Revogar(agora);
        foreach (var link in await _acessos.LinksNaoRevogadosAsync(contato.Id, cancellationToken))
            link.Revogar(agora);

        if (contato.UsuarioId is not { } usuarioId)
            return;

        // Só o vínculo deste cliente: a mesma pessoa pode ser da equipe em outro tenant, nunca neste (um vínculo por tenant).
        var vinculo = await _vinculos.DoUsuarioAsync(usuarioId, cancellationToken);
        if (vinculo is not null && vinculo.ClienteId == cliente.Id)
        {
            _repositorioVinculos.Remover(vinculo);
            await _vinculos.RevogarSessoesAsync(usuarioId, agora, cancellationToken);
        }

        cliente.DesvincularUsuarioDoContato(contato.Id);
    }

    public async Task RevogarTodosAsync(Cliente cliente, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        foreach (var contato in cliente.Contatos.ToList())
            await RevogarAsync(cliente, contato.Id, cancellationToken);
    }
}
