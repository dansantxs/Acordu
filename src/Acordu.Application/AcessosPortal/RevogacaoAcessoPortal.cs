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
    private readonly IRepositorio<Vinculo> _vinculos;
    private readonly RelogioDoTenant _relogio;

    public RevogacaoAcessoPortal(IAcessosDoPortal acessos, IRepositorio<Vinculo> vinculos, RelogioDoTenant relogio)
    {
        ArgumentNullException.ThrowIfNull(acessos);
        ArgumentNullException.ThrowIfNull(vinculos);
        ArgumentNullException.ThrowIfNull(relogio);
        _acessos = acessos;
        _vinculos = vinculos;
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
        var vinculo = await _acessos.VinculoAsync(usuarioId, cancellationToken);
        if (vinculo is not null && vinculo.ClienteId == cliente.Id)
        {
            _vinculos.Remover(vinculo);
            await _acessos.RevogarSessoesAsync(usuarioId, agora, cancellationToken);
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
