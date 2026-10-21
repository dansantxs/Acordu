using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Services;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Recorrencias;

/// <summary>
/// Casos de uso de tarefas recorrentes (contrato §10). Editar o modelo vale só para as próximas tarefas: as já geradas
/// não mudam. A geração das tarefas é do job (agendador), não daqui.
/// </summary>
public sealed class ComandosRecorrencia
{
    public const int PreviaPadrao = 6;
    public const int PreviaMaxima = 24;

    private readonly ITenantContext _contexto;
    private readonly IRepositorio<RecorrenciaTarefa> _recorrencias;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<Contrato> _contratos;
    private readonly IMembrosDaEquipe _equipe;
    private readonly IUnitOfWork _unidade;
    private readonly IControleVersao _versoes;
    private readonly RelogioDoTenant _relogio;

    public ComandosRecorrencia(
        ITenantContext contexto,
        IRepositorio<RecorrenciaTarefa> recorrencias,
        IRepositorio<Cliente> clientes,
        IRepositorio<Contrato> contratos,
        IMembrosDaEquipe equipe,
        IUnitOfWork unidade,
        IControleVersao versoes,
        RelogioDoTenant relogio)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(recorrencias);
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(contratos);
        ArgumentNullException.ThrowIfNull(equipe);
        ArgumentNullException.ThrowIfNull(unidade);
        ArgumentNullException.ThrowIfNull(versoes);
        ArgumentNullException.ThrowIfNull(relogio);
        _contexto = contexto;
        _recorrencias = recorrencias;
        _clientes = clientes;
        _contratos = contratos;
        _equipe = equipe;
        _unidade = unidade;
        _versoes = versoes;
        _relogio = relogio;
    }

    public async Task<Guid> CriarAsync(NovaRecorrenciaRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.RecorrenciaGerenciar);
        var cliente = await _clientes.ObterPorIdAsync(dados.ClienteId, cancellationToken)
            ?? throw ErroDeCampo.Criar("clienteId", "Cliente não encontrado.");
        if (!cliente.Ativo)
            throw ErroDeCampo.Criar("clienteId", "Cliente inativo não recebe novas recorrências.");
        await ValidarContratoAsync(dados.ContratoId, cliente.Id, cancellationToken);
        var modelo = await ModeloAsync(dados.Modelo, null, cancellationToken);
        var hoje = await _relogio.HojeAsync(cancellationToken);

        var recorrencia = RecorrenciaTarefa.Criar(
            Guid.CreateVersion7(),
            _contexto.TenantId!.Value,
            cliente.Id,
            dados.ContratoId,
            modelo,
            Regra(dados.Regra, hoje),
            hoje);
        _recorrencias.Adicionar(recorrencia);
        await _unidade.SalvarAsync(cancellationToken);
        return recorrencia.Id;
    }

    public async Task EditarAsync(Guid id, EdicaoRecorrenciaRequest dados, string? versao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var recorrencia = await CarregarAsync(id, versao, cancellationToken);
        if (dados.ContratoId != recorrencia.ContratoId)
            await ValidarContratoAsync(dados.ContratoId, recorrencia.ClienteId, cancellationToken);
        var modelo = await ModeloAsync(dados.Modelo, recorrencia.Modelo.ResponsavelId, cancellationToken);
        var hoje = await _relogio.HojeAsync(cancellationToken);
        var regra = Regra(dados.Regra, recorrencia.Regra.Inicio);

        recorrencia.AlterarContrato(dados.ContratoId);
        recorrencia.AlterarModelo(modelo);
        // Regra igual não recalcula: recalcular "a partir de hoje" pularia uma ocorrência devida ainda não gerada.
        if (regra != recorrencia.Regra)
            recorrencia.AlterarRegra(regra, hoje);
        await _unidade.SalvarAsync(cancellationToken);
    }

    public Task PausarAsync(Guid id, string? versao, CancellationToken cancellationToken = default) =>
        AlterarAsync(id, versao, (r, _) => r.Pausar(), cancellationToken);

    public Task RetomarAsync(Guid id, string? versao, CancellationToken cancellationToken = default) =>
        AlterarAsync(id, versao, (r, hoje) => r.Retomar(hoje), cancellationToken);

    public Task EncerrarAsync(Guid id, string? versao, CancellationToken cancellationToken = default) =>
        AlterarAsync(id, versao, (r, _) => r.Encerrar(), cancellationToken);

    /// <summary>
    /// Próximas datas de referência (contrato §10), para o usuário conferir o clamp de fim de mês. ATIVA: a partir da
    /// próxima data; PAUSADA: as que valeriam se retomasse hoje; ENCERRADA: nenhuma.
    /// </summary>
    public async Task<IReadOnlyList<DateOnly>> PreviaAsync(Guid id, int? quantidade, CancellationToken cancellationToken = default)
    {
        _contexto.Exigir(Permissoes.RecorrenciaVer);
        var total = quantidade ?? PreviaPadrao;
        if (total < 1 || total > PreviaMaxima)
            throw ErroDeCampo.Criar("quantidade", $"Quantidade entre 1 e {PreviaMaxima}.");
        var recorrencia = await _recorrencias.ObterPorIdAsync(id, cancellationToken)
            ?? throw new NaoEncontradoException("Recorrência não encontrada.");

        var proxima = recorrencia.Status switch
        {
            StatusRecorrencia.Ativa => recorrencia.ProximaData,
            StatusRecorrencia.Pausada => GeradorDeDatas.Proxima(recorrencia.Regra, await _relogio.HojeAsync(cancellationToken)),
            _ => null,
        };
        var datas = new List<DateOnly>(total);
        while (proxima is { } data && datas.Count < total)
        {
            datas.Add(data);
            proxima = GeradorDeDatas.Proxima(recorrencia.Regra, data.AddDays(1));
        }

        return datas;
    }

    /// <summary>
    /// Próximas datas de uma regra sem salvar nada (decidido em 21/10, F4c): o formulário mostra o que a regra vai
    /// gerar, com o mesmo gerador da recorrência salva. Conta a partir de hoje (ou do início, se for depois).
    /// </summary>
    public async Task<IReadOnlyList<DateOnly>> PreviaDaRegraAsync(PreviaRegraRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.RecorrenciaVer);
        var total = dados.Quantidade ?? PreviaPadrao;
        if (total < 1 || total > PreviaMaxima)
            throw ErroDeCampo.Criar("quantidade", $"Quantidade entre 1 e {PreviaMaxima}.");
        var hoje = await _relogio.HojeAsync(cancellationToken);
        var regra = Regra(dados.Regra, hoje);

        var datas = new List<DateOnly>(total);
        var proxima = GeradorDeDatas.Proxima(regra, hoje);
        while (proxima is { } data && datas.Count < total)
        {
            datas.Add(data);
            proxima = GeradorDeDatas.Proxima(regra, data.AddDays(1));
        }

        return datas;
    }

    private async Task AlterarAsync(
        Guid id, string? versao, Action<RecorrenciaTarefa, DateOnly> alterar, CancellationToken cancellationToken)
    {
        var recorrencia = await CarregarAsync(id, versao, cancellationToken);
        alterar(recorrencia, await _relogio.HojeAsync(cancellationToken));
        await _unidade.SalvarAsync(cancellationToken);
    }

    private async Task<RecorrenciaTarefa> CarregarAsync(Guid id, string? versao, CancellationToken cancellationToken)
    {
        _contexto.Exigir(Permissoes.RecorrenciaGerenciar);
        var recorrencia = await _recorrencias.ObterPorIdAsync(id, cancellationToken)
            ?? throw new NaoEncontradoException("Recorrência não encontrada.");
        _versoes.Esperar(recorrencia, versao);
        return recorrencia;
    }

    private async Task ValidarContratoAsync(Guid? contratoId, Guid clienteId, CancellationToken cancellationToken)
    {
        if (contratoId is not { } id)
            return;
        var contrato = await _contratos.ObterPorIdAsync(id, cancellationToken);
        if (contrato is null || contrato.ClienteId != clienteId)
            throw ErroDeCampo.Criar("contratoId", "Contrato não encontrado para este cliente.");
        if (contrato.Status == StatusContrato.Encerrado)
            throw ErroDeCampo.Criar("contratoId", "Contrato encerrado não recebe recorrências.");
    }

    /// <summary>Responsável checado só quando muda: quem saiu da equipe não trava a edição do resto (a geração o ignora).</summary>
    private async Task<ModeloTarefa> ModeloAsync(ModeloTarefaDto? dados, Guid? responsavelAtual, CancellationToken cancellationToken)
    {
        if (dados is null)
            throw ErroDeCampo.Criar("modelo", "Informe o modelo da tarefa.");
        if (dados.ResponsavelId is { } responsavel && responsavel != responsavelAtual
            && !await _equipe.EhDaEquipeAsync(responsavel, cancellationToken))
            throw ErroDeCampo.Criar("modelo.responsavelId", "O responsável precisa ser da equipe.");
        if (dados.EstimativaMinutos is <= 0)
            throw ErroDeCampo.Criar("modelo.estimativaMinutos", "A estimativa precisa ser maior que zero.");

        return NoCampo("modelo", () => new ModeloTarefa(
            dados.Titulo,
            dados.Descricao,
            dados.ResponsavelId,
            dados.Checklist ?? [],
            dados.EstimativaMinutos is { } minutos ? Duracao.FromMinutos(minutos) : null,
            dados.VisivelCliente));
    }

    private static RegraRecorrencia Regra(RegraRecorrenciaDto? dados, DateOnly inicioPadrao)
    {
        if (dados is null)
            throw ErroDeCampo.Criar("regra", "Informe a regra da recorrência.");
        return NoCampo("regra", () => new RegraRecorrencia(
            dados.Periodicidade, dados.DiaReferencia, dados.AntecedenciaDias, dados.Inicio ?? inicioPadrao, dados.Fim));
    }

    /// <summary>Erro de validação do VO com o caminho do campo no JSON (<c>regra.diaReferencia</c>), sem o sufixo do .NET.</summary>
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
