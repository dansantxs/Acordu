using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Application.AcessosPortal;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Clientes;

/// <summary>Casos de uso de escrita de clientes e contatos (contrato §4.1–4.2). Devolvem o Id; a leitura é das consultas.</summary>
public sealed class ComandosCliente
{
    private readonly ITenantContext _contexto;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IUnitOfWork _unidade;
    private readonly IControleVersao _versoes;
    private readonly RevogacaoAcessoPortal _revogacao;
    private readonly IMembrosDaEquipe _equipe;

    public ComandosCliente(
        ITenantContext contexto,
        IRepositorio<Cliente> clientes,
        IUnitOfWork unidade,
        IControleVersao versoes,
        RevogacaoAcessoPortal revogacao,
        IMembrosDaEquipe equipe)
    {
        ArgumentNullException.ThrowIfNull(equipe);
        _equipe = equipe;
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(unidade);
        ArgumentNullException.ThrowIfNull(versoes);
        ArgumentNullException.ThrowIfNull(revogacao);
        _contexto = contexto;
        _clientes = clientes;
        _unidade = unidade;
        _versoes = versoes;
        _revogacao = revogacao;
    }

    public async Task<Guid> CriarAsync(ClienteRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.ClienteGerenciar);
        var cliente = Cliente.Criar(Guid.CreateVersion7(), _contexto.TenantId!.Value, dados.Nome, dados.Documento, dados.Observacoes);
        await AplicarCadastroAsync(cliente, dados, cancellationToken);
        _clientes.Adicionar(cliente);
        await _unidade.SalvarAsync(cancellationToken);
        return cliente.Id;
    }

    public async Task EditarAsync(Guid id, ClienteRequest dados, string? versao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var cliente = await CarregarAsync(id, versao, cancellationToken);
        cliente.Editar(dados.Nome, dados.Documento, dados.Observacoes);
        await AplicarCadastroAsync(cliente, dados, cancellationToken);
        await _unidade.SalvarAsync(cancellationToken);
    }

    /// <summary>Inativação lógica: o histórico fica e os acessos de todos os contatos ao portal são revogados.</summary>
    public async Task InativarAsync(Guid id, string? versao, CancellationToken cancellationToken = default)
    {
        var cliente = await CarregarAsync(id, versao, cancellationToken);
        cliente.Inativar();
        await _revogacao.RevogarTodosAsync(cliente, cancellationToken);
        await _unidade.SalvarAsync(cancellationToken);
    }

    /// <summary>Cliente que voltou (16/10). Os acessos ao portal revogados na inativação não voltam; convida-se de novo.</summary>
    public async Task ReativarAsync(Guid id, string? versao, CancellationToken cancellationToken = default)
    {
        var cliente = await CarregarAsync(id, versao, cancellationToken);
        cliente.Reativar();
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

    /// <summary>Inativa o contato e revoga o acesso dele ao portal (vínculo, sessões, convites e links).</summary>
    public async Task InativarContatoAsync(Guid id, Guid contatoId, string? versao, CancellationToken cancellationToken = default)
    {
        var cliente = await CarregarComContatoAsync(id, contatoId, versao, cancellationToken);
        cliente.InativarContato(contatoId);
        await _revogacao.RevogarAsync(cliente, contatoId, cancellationToken);
        await _unidade.SalvarAsync(cancellationToken);
    }

    public async Task ReativarContatoAsync(Guid id, Guid contatoId, string? versao, CancellationToken cancellationToken = default)
    {
        var cliente = await CarregarComContatoAsync(id, contatoId, versao, cancellationToken);
        cliente.ReativarContato(contatoId);
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

    /// <summary>
    /// Campos do cadastro ampliado (16/10). O responsável só é conferido quando muda: quem saiu da equipe continua
    /// aparecendo até alguém trocar, sem travar a edição dos outros campos.
    /// </summary>
    private async Task AplicarCadastroAsync(Cliente cliente, ClienteRequest dados, CancellationToken cancellationToken)
    {
        var endereco = dados.Endereco is { } e
            ? NoCampo("endereco", () => new Endereco(e.Cep, e.Logradouro, e.Numero, e.Complemento, e.Bairro, e.Cidade, e.Uf))
            : null;
        cliente.DefinirCadastro(dados.RazaoSocial, dados.InscricaoEstadual, dados.Email, dados.Telefone, endereco);
        cliente.DefinirEtiquetas(dados.Etiquetas);
        if (dados.ResponsavelId != cliente.ResponsavelId)
        {
            if (dados.ResponsavelId is { } id && !await _equipe.EhDaEquipeAsync(id, cancellationToken))
                throw ErroDeCampo.Criar("responsavelId", "O responsável precisa ser da equipe.");
            cliente.DefinirResponsavel(dados.ResponsavelId);
        }
    }

    /// <summary>Erro de validação do VO com o caminho do campo no JSON (<c>endereco.cep</c>), sem o sufixo do .NET.</summary>
    private static T NoCampo<T>(string objeto, Func<T> criar)
    {
        try
        {
            return criar();
        }
        catch (ArgumentException e) when (e.ParamName is not null)
        {
            var mensagem = e.Message.Split('\n')[0].Replace($" (Parameter '{e.ParamName}')", "", StringComparison.Ordinal).Trim();
            throw ErroDeCampo.Criar($"{objeto}.{e.ParamName}", mensagem);
        }
    }
}
