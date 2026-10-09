using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Application.Equipe;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;

namespace Acordu.Application.Papeis;

/// <summary>Papel do tenant com as permissões e quantas pessoas o usam.</summary>
public sealed record PapelDto(
    Guid Id, string Nome, TipoPapel Tipo, bool Padrao, IReadOnlyList<string> Permissoes, int Pessoas);

/// <summary>
/// Corpo do <c>POST</c> e do <c>PATCH /papeis</c> (o formulário inteiro). Papel sob medida é sempre INTERNO: o papel de
/// cliente é o padrão, igual para todos os contatos.
/// </summary>
public sealed record PapelRequest(string Nome, IReadOnlyList<string>? Permissoes);

public interface IConsultasPapel
{
    /// <summary>Padrões primeiro (na ordem do §3.2), depois os sob medida por nome.</summary>
    Task<IReadOnlyList<PapelDto>> ListarAsync(CancellationToken cancellationToken = default);

    Task<PapelDto?> DetalharAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Nome já usado no tenant (sem diferenciar maiúsculas), fora <paramref name="ignorar"/>.</summary>
    Task<bool> NomeEmUsoAsync(string nome, Guid? ignorar, CancellationToken cancellationToken = default);
}

/// <summary>
/// Papéis sob medida (contrato §3, segurança §3.3): criar, editar e excluir. Padrão não muda; em uso não é excluído;
/// excluir revoga os convites pendentes com ele. Mudança de permissão vale no próximo refresh de quem tem o papel.
/// </summary>
public sealed class ComandosPapel
{
    private readonly ITenantContext _contexto;
    private readonly IRepositorio<Papel> _papeis;
    private readonly IConsultasPapel _consultas;
    private readonly IVinculos _vinculos;
    private readonly IConvitesEquipe _convites;
    private readonly RelogioDoTenant _relogio;
    private readonly IUnitOfWork _unidade;

    public ComandosPapel(
        ITenantContext contexto,
        IRepositorio<Papel> papeis,
        IConsultasPapel consultas,
        IVinculos vinculos,
        IConvitesEquipe convites,
        RelogioDoTenant relogio,
        IUnitOfWork unidade)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(papeis);
        ArgumentNullException.ThrowIfNull(consultas);
        ArgumentNullException.ThrowIfNull(vinculos);
        ArgumentNullException.ThrowIfNull(convites);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(unidade);
        _contexto = contexto;
        _papeis = papeis;
        _consultas = consultas;
        _vinculos = vinculos;
        _convites = convites;
        _relogio = relogio;
        _unidade = unidade;
    }

    /// <summary>Catálogo de permissões que um papel sob medida pode ter (as internas, §3.1), em ordem.</summary>
    public static IReadOnlyList<string> Catalogo { get; } = [.. Permissoes.Internas.Order(StringComparer.Ordinal)];

    public async Task<Guid> CriarAsync(PapelRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.PapelGerenciar);
        await GarantirNomeLivreAsync(dados.Nome, null, cancellationToken);
        var papel = Papel.Criar(Guid.CreateVersion7(), _contexto.TenantId!.Value, dados.Nome, TipoPapel.Interno, dados.Permissoes ?? []);
        _papeis.Adicionar(papel);
        await _unidade.SalvarAsync(cancellationToken);
        return papel.Id;
    }

    public async Task EditarAsync(Guid id, PapelRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var papel = await CarregarAsync(id, cancellationToken);
        if (papel.Padrao)
            throw new TransicaoInvalidaException("Papel padrão não pode ser alterado. Crie um papel sob medida.");
        await GarantirNomeLivreAsync(dados.Nome, papel.Id, cancellationToken);
        papel.Renomear(dados.Nome);
        papel.DefinirPermissoes(dados.Permissoes ?? []);
        await _unidade.SalvarAsync(cancellationToken);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var papel = await CarregarAsync(id, cancellationToken);
        if (papel.Padrao)
            throw new TransicaoInvalidaException("Papel padrão não pode ser excluído.");
        if (await _vinculos.ContarPorPapelAsync(papel.Id, cancellationToken) > 0)
            throw new EquipeException("O papel está em uso. Troque o papel dessas pessoas antes de excluir.");

        var agora = _relogio.Agora;
        foreach (var convite in await _convites.PendentesDoPapelAsync(papel.Id, cancellationToken))
            convite.Revogar(agora);
        _papeis.Remover(papel);
        await _unidade.SalvarAsync(cancellationToken);
    }

    private async Task<Papel> CarregarAsync(Guid id, CancellationToken cancellationToken)
    {
        _contexto.Exigir(Permissoes.PapelGerenciar);
        return await _papeis.ObterPorIdAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Papel não encontrado.");
    }

    private async Task GarantirNomeLivreAsync(string? nome, Guid? ignorar, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(nome) && await _consultas.NomeEmUsoAsync(nome, ignorar, cancellationToken))
            throw ErroDeCampo.Criar("nome", "Já existe um papel com este nome.");
    }
}
