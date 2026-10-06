using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Application.Contratos;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Solicitacoes;

/// <summary>
/// Casos de uso de escrita de solicitações e interações da equipe (contrato §7). As regras entre agregados da
/// interação (primeira resposta, retomada) ficam no <see cref="PrimeiraRespostaERetomada"/>, na mesma transação.
/// </summary>
public sealed class ComandosSolicitacao
{
    /// <summary>Política padrão do contrato da API (5 anexos de até 20 MB); anexos entram no marco de anexos.</summary>
    internal static readonly PoliticaAnexos PoliticaPadrao = new(
        20L * 1024 * 1024,
        ["application/pdf", "image/png", "image/jpeg", "text/plain", "application/zip"],
        5);

    private readonly ITenantContext _contexto;
    private readonly IRepositorio<Solicitacao> _solicitacoes;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<Contrato> _contratos;
    private readonly IRepositorio<Categoria> _categorias;
    private readonly IRepositorio<Interacao> _interacoes;
    private readonly IConsultasContrato _consultasContrato;
    private readonly IMembrosDaEquipe _equipe;
    private readonly IGeradorProtocolo _protocolo;
    private readonly ICalendarios _calendarios;
    private readonly RelogioDoTenant _relogio;
    private readonly IUnitOfWork _unidade;
    private readonly IControleVersao _versoes;

    public ComandosSolicitacao(
        ITenantContext contexto,
        IRepositorio<Solicitacao> solicitacoes,
        IRepositorio<Cliente> clientes,
        IRepositorio<Contrato> contratos,
        IRepositorio<Categoria> categorias,
        IRepositorio<Interacao> interacoes,
        IConsultasContrato consultasContrato,
        IMembrosDaEquipe equipe,
        IGeradorProtocolo protocolo,
        ICalendarios calendarios,
        RelogioDoTenant relogio,
        IUnitOfWork unidade,
        IControleVersao versoes)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(solicitacoes);
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(contratos);
        ArgumentNullException.ThrowIfNull(categorias);
        ArgumentNullException.ThrowIfNull(interacoes);
        ArgumentNullException.ThrowIfNull(consultasContrato);
        ArgumentNullException.ThrowIfNull(equipe);
        ArgumentNullException.ThrowIfNull(protocolo);
        ArgumentNullException.ThrowIfNull(calendarios);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(unidade);
        ArgumentNullException.ThrowIfNull(versoes);
        _contexto = contexto;
        _solicitacoes = solicitacoes;
        _clientes = clientes;
        _contratos = contratos;
        _categorias = categorias;
        _interacoes = interacoes;
        _consultasContrato = consultasContrato;
        _equipe = equipe;
        _protocolo = protocolo;
        _calendarios = calendarios;
        _relogio = relogio;
        _unidade = unidade;
        _versoes = versoes;
    }

    /// <summary>
    /// Abre com origem INTERNA. Contrato informado precisa ser do cliente e vigente hoje (422); sem contrato, usa o
    /// único ATIVO vigente do cliente, se houver exatamente um; senão fica sem SLA.
    /// </summary>
    public async Task<Guid> CriarAsync(NovaSolicitacaoRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.SolicitacaoCriar);
        var cliente = await _clientes.ObterPorIdAsync(dados.ClienteId, cancellationToken)
            ?? throw ErroDeCampo.Criar("clienteId", "Cliente não encontrado.");
        if (!cliente.Ativo)
            throw ErroDeCampo.Criar("clienteId", "Cliente inativo não recebe novas solicitações.");
        if (dados.ContatoId is { } contatoId && !cliente.Contatos.Any(c => c.Id == contatoId && c.Ativo))
            throw ErroDeCampo.Criar("contatoId", "Contato não encontrado entre os ativos do cliente.");
        await ValidarCategoriaAsync(dados.CategoriaId, cancellationToken);
        await ValidarResponsavelAsync(dados.ResponsavelId, cancellationToken);

        var hoje = await _relogio.HojeAsync(cancellationToken);
        var contrato = await ContratoDaSolicitacaoAsync(cliente.Id, dados.ContratoId, hoje, cancellationToken);
        var calendario = await _calendarios.DoTenantAsync(cancellationToken);
        var numero = await _protocolo.ProximoNumeroSolicitacaoAsync(cancellationToken);

        var solicitacao = Solicitacao.Abrir(
            Guid.CreateVersion7(),
            _contexto.TenantId!.Value,
            cliente.Id,
            numero,
            dados.Titulo,
            dados.Descricao,
            dados.Prioridade,
            OrigemSolicitacao.Interna,
            dados.ContatoId,
            dados.CategoriaId,
            dados.ResponsavelId,
            contrato?.Id,
            contrato?.Sla,
            _relogio.Agora,
            calendario);
        _solicitacoes.Adicionar(solicitacao);
        await _unidade.SalvarAsync(cancellationToken);
        return solicitacao.Id;
    }

    public async Task EditarAsync(Guid id, EdicaoSolicitacaoRequest dados, string? versao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var solicitacao = await CarregarAsync(id, Permissoes.SolicitacaoEditar, versao, cancellationToken);
        if (dados.CategoriaId != solicitacao.CategoriaId)
            await ValidarCategoriaAsync(dados.CategoriaId, cancellationToken);
        if (dados.ResponsavelId != solicitacao.ResponsavelId)
            await ValidarResponsavelAsync(dados.ResponsavelId, cancellationToken);

        solicitacao.Editar(dados.Titulo, dados.Descricao);
        solicitacao.DefinirCategoria(dados.CategoriaId);
        solicitacao.Atribuir(dados.ResponsavelId);
        if (dados.Prioridade != solicitacao.Prioridade)
        {
            var politica = solicitacao.ContratoId is { } contratoId
                ? (await _contratos.ObterPorIdAsync(contratoId, cancellationToken))?.Sla
                : null;
            solicitacao.Repriorizar(dados.Prioridade, politica, await _calendarios.DoTenantAsync(cancellationToken));
        }

        await _unidade.SalvarAsync(cancellationToken);
    }

    public Task IniciarAsync(Guid id, string? versao, CancellationToken cancellationToken = default) =>
        TransicaoAsync(id, versao, (s, agora, _) => s.Iniciar(agora), cancellationToken);

    public Task AguardarClienteAsync(Guid id, string? versao, CancellationToken cancellationToken = default) =>
        TransicaoAsync(id, versao, (s, agora, _) => s.AguardarCliente(agora), cancellationToken);

    public Task AguardarTerceiroAsync(Guid id, string? versao, CancellationToken cancellationToken = default) =>
        TransicaoAsync(id, versao, (s, agora, _) => s.AguardarTerceiro(agora), cancellationToken);

    public Task RetomarAsync(Guid id, string? versao, CancellationToken cancellationToken = default) =>
        TransicaoAsync(id, versao, (s, agora, calendario) => s.Retomar(agora, calendario), cancellationToken);

    public Task ResolverAsync(Guid id, SolucaoDto? solucao, string? versao, CancellationToken cancellationToken = default)
    {
        if (solucao is null)
            throw ErroDeCampo.Criar("solucao", "A solução é obrigatória para resolver.");
        var valor = new Solucao(solucao.Texto, solucao.Problema, solucao.Causa);
        return TransicaoAsync(id, versao, (s, agora, calendario) => s.Resolver(valor, agora, calendario), cancellationToken);
    }

    public Task ReabrirAsync(Guid id, string motivo, string? versao, CancellationToken cancellationToken = default) =>
        TransicaoAsync(id, versao, (s, agora, calendario) => s.Reabrir(motivo, agora, calendario), cancellationToken);

    public Task CancelarAsync(Guid id, string motivo, string? versao, CancellationToken cancellationToken = default) =>
        TransicaoAsync(id, versao, (s, agora, _) => s.Cancelar(motivo, agora), cancellationToken);

    /// <summary>Comentário da equipe. Interação EXTERNA registra a primeira resposta (§3.1), na mesma transação.</summary>
    public async Task<Guid> ComentarAsync(Guid solicitacaoId, InteracaoRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var solicitacao = await CarregarAsync(solicitacaoId, Permissoes.SolicitacaoComentar, null, cancellationToken);
        var interacao = Interacao.Registrar(
            Guid.CreateVersion7(),
            solicitacao.TenantId,
            solicitacao.ClienteId,
            solicitacao.Id,
            _contexto.Usuario(),
            autorEhCliente: false,
            dados.Texto,
            dados.Visibilidade ?? Visibilidade.Interna,
            [],
            PoliticaPadrao,
            _relogio.Agora);
        _interacoes.Adicionar(interacao);
        await _unidade.SalvarAsync(cancellationToken);
        return interacao.Id;
    }

    private async Task TransicaoAsync(
        Guid id, string? versao, Action<Solicitacao, DateTimeOffset, CalendarioUtil> transicao, CancellationToken cancellationToken)
    {
        var solicitacao = await CarregarAsync(id, Permissoes.SolicitacaoEditar, versao, cancellationToken);
        transicao(solicitacao, _relogio.Agora, await _calendarios.DoTenantAsync(cancellationToken));
        await _unidade.SalvarAsync(cancellationToken);
    }

    private async Task<Solicitacao> CarregarAsync(Guid id, string permissao, string? versao, CancellationToken cancellationToken)
    {
        _contexto.Exigir(permissao);
        var solicitacao = await _solicitacoes.ObterPorIdAsync(id, cancellationToken)
            ?? throw new NaoEncontradoException("Solicitação não encontrada.");
        _versoes.Esperar(solicitacao, versao);
        return solicitacao;
    }

    private async Task<Contrato?> ContratoDaSolicitacaoAsync(
        Guid clienteId, Guid? contratoId, DateOnly hoje, CancellationToken cancellationToken)
    {
        if (contratoId is { } informado)
        {
            var contrato = await _contratos.ObterPorIdAsync(informado, cancellationToken)
                ?? throw ErroDeCampo.Criar("contratoId", "Contrato não encontrado.");
            if (contrato.ClienteId != clienteId)
                throw new ContratoIncoerenteException("O contrato é de outro cliente.");
            if (!contrato.EstaVigenteEm(hoje))
                throw new ContratoIncoerenteException("O contrato não está vigente.");
            return contrato;
        }

        var vigentes = await _consultasContrato.VigentesDoClienteAsync(clienteId, hoje, cancellationToken);
        return vigentes.Count == 1 ? await _contratos.ObterPorIdAsync(vigentes[0], cancellationToken) : null;
    }

    private async Task ValidarCategoriaAsync(Guid? categoriaId, CancellationToken cancellationToken)
    {
        if (categoriaId is not { } id)
            return;
        var categoria = await _categorias.ObterPorIdAsync(id, cancellationToken);
        if (categoria is null || !categoria.Ativa)
            throw ErroDeCampo.Criar("categoriaId", "Categoria não encontrada entre as ativas.");
    }

    private async Task ValidarResponsavelAsync(Guid? responsavelId, CancellationToken cancellationToken)
    {
        if (responsavelId is { } id && !await _equipe.EhDaEquipeAsync(id, cancellationToken))
            throw ErroDeCampo.Criar("responsavelId", "O responsável precisa ser da equipe.");
    }
}
