using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;

namespace Acordu.Application.Categorias;

public sealed record CategoriaDto(Guid Id, string Nome, bool Ativa);

/// <summary>Corpo do <c>POST</c> e do <c>PATCH /categorias</c>; <see cref="Ativa"/> nulo mantém (no POST, nasce ativa).</summary>
public sealed record CategoriaRequest(string Nome, bool? Ativa);

/// <summary>Leituras de categorias do tenant.</summary>
public interface IConsultasCategoria
{
    Task<IReadOnlyList<CategoriaDto>> ListarAsync(bool? ativa, CancellationToken cancellationToken = default);

    Task<CategoriaDto?> DetalharAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Nome já usado no tenant (sem diferenciar maiúsculas), fora <paramref name="ignorar"/>.</summary>
    Task<bool> NomeEmUsoAsync(string nome, Guid? ignorar, CancellationToken cancellationToken = default);
}

/// <summary>
/// Categorias de solicitação (contrato §6). Nome único por tenant é regra entre agregados (modelo §4.12): checada aqui,
/// com o índice único do banco como última barreira.
/// </summary>
public sealed class ComandosCategoria
{
    private readonly ITenantContext _contexto;
    private readonly IRepositorio<Categoria> _categorias;
    private readonly IConsultasCategoria _consultas;
    private readonly IUnitOfWork _unidade;

    public ComandosCategoria(
        ITenantContext contexto, IRepositorio<Categoria> categorias, IConsultasCategoria consultas, IUnitOfWork unidade)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(categorias);
        ArgumentNullException.ThrowIfNull(consultas);
        ArgumentNullException.ThrowIfNull(unidade);
        _contexto = contexto;
        _categorias = categorias;
        _consultas = consultas;
        _unidade = unidade;
    }

    public async Task<Guid> CriarAsync(CategoriaRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.ConfiguracaoGerenciar);
        var categoria = Categoria.Criar(Guid.CreateVersion7(), _contexto.TenantId!.Value, dados.Nome);
        await GarantirNomeLivreAsync(categoria.Nome, null, cancellationToken);
        if (dados.Ativa is false)
            categoria.Inativar();
        _categorias.Adicionar(categoria);
        await _unidade.SalvarAsync(cancellationToken);
        return categoria.Id;
    }

    public async Task EditarAsync(Guid id, CategoriaRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var categoria = await CarregarAsync(id, cancellationToken);
        categoria.Renomear(dados.Nome);
        await GarantirNomeLivreAsync(categoria.Nome, categoria.Id, cancellationToken);
        if (dados.Ativa is true)
            categoria.Reativar();
        else if (dados.Ativa is false)
            categoria.Inativar();
        await _unidade.SalvarAsync(cancellationToken);
    }

    public async Task InativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var categoria = await CarregarAsync(id, cancellationToken);
        categoria.Inativar();
        await _unidade.SalvarAsync(cancellationToken);
    }

    private async Task<Categoria> CarregarAsync(Guid id, CancellationToken cancellationToken)
    {
        _contexto.Exigir(Permissoes.ConfiguracaoGerenciar);
        return await _categorias.ObterPorIdAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Categoria não encontrada.");
    }

    private async Task GarantirNomeLivreAsync(string nome, Guid? ignorar, CancellationToken cancellationToken)
    {
        if (await _consultas.NomeEmUsoAsync(nome, ignorar, cancellationToken))
            throw ErroDeCampo.Criar("nome", "Já existe uma categoria com esse nome.");
    }
}
