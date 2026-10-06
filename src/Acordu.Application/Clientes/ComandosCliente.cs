using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;

namespace Acordu.Application.Clientes;

/// <summary>Casos de uso de escrita de clientes e contatos (contrato §4.1–4.2). Devolvem o Id; a leitura é das consultas.</summary>
public sealed class ComandosCliente
{
    private readonly ITenantContext _contexto;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IUnitOfWork _unidade;
    private readonly IControleVersao _versoes;

    public ComandosCliente(ITenantContext contexto, IRepositorio<Cliente> clientes, IUnitOfWork unidade, IControleVersao versoes)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(unidade);
        ArgumentNullException.ThrowIfNull(versoes);
        _contexto = contexto;
        _clientes = clientes;
        _unidade = unidade;
        _versoes = versoes;
    }

    public async Task<Guid> CriarAsync(ClienteRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.ClienteGerenciar);
        var cliente = Cliente.Criar(Guid.CreateVersion7(), _contexto.TenantId!.Value, dados.Nome, dados.Documento, dados.Observacoes);
        _clientes.Adicionar(cliente);
        await _unidade.SalvarAsync(cancellationToken);
        return cliente.Id;
    }

    public async Task EditarAsync(Guid id, ClienteRequest dados, string? versao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var cliente = await CarregarAsync(id, versao, cancellationToken);
        cliente.Editar(dados.Nome, dados.Documento, dados.Observacoes);
        await _unidade.SalvarAsync(cancellationToken);
    }

    /// <summary>Inativação lógica: o histórico fica. Revogar acessos ao portal entra com o portal (6d).</summary>
    public async Task InativarAsync(Guid id, string? versao, CancellationToken cancellationToken = default)
    {
        var cliente = await CarregarAsync(id, versao, cancellationToken);
        cliente.Inativar();
        await _unidade.SalvarAsync(cancellationToken);
    }

    public async Task<Guid> AdicionarContatoAsync(Guid id, ContatoRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var cliente = await CarregarAsync(id, null, cancellationToken);
        var contato = cliente.AdicionarContato(dados.Nome, dados.Email, dados.Telefone, dados.Cargo);
        await _unidade.SalvarAsync(cancellationToken);
        return contato.Id;
    }

    public async Task EditarContatoAsync(
        Guid id, Guid contatoId, ContatoRequest dados, string? versao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var cliente = await CarregarComContatoAsync(id, contatoId, versao, cancellationToken);
        cliente.AtualizarContato(contatoId, dados.Nome, dados.Email, dados.Telefone, dados.Cargo);
        await _unidade.SalvarAsync(cancellationToken);
    }

    public async Task InativarContatoAsync(Guid id, Guid contatoId, string? versao, CancellationToken cancellationToken = default)
    {
        var cliente = await CarregarComContatoAsync(id, contatoId, versao, cancellationToken);
        cliente.InativarContato(contatoId);
        await _unidade.SalvarAsync(cancellationToken);
    }

    private async Task<Cliente> CarregarAsync(Guid id, string? versao, CancellationToken cancellationToken)
    {
        _contexto.Exigir(Permissoes.ClienteGerenciar);
        var cliente = await _clientes.ObterPorIdAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Cliente não encontrado.");
        _versoes.Esperar(cliente, versao);
        return cliente;
    }

    private async Task<Cliente> CarregarComContatoAsync(Guid id, Guid contatoId, string? versao, CancellationToken cancellationToken)
    {
        var cliente = await CarregarAsync(id, versao, cancellationToken);
        if (!cliente.Contatos.Any(c => c.Id == contatoId))
            throw new NaoEncontradoException("Contato não encontrado.");
        return cliente;
    }
}
