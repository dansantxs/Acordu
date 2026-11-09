using Acordu.Application.Abstractions;
using Acordu.Application.Auditoria;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Apontamentos;

/// <summary>
/// Apontamentos e cronômetro (contrato §9). O contrato vem da tarefa (solicitação, projeto ou recorrência) e fica
/// congelado; os meses fechados são os do contrato congelado. Editar e excluir: autor ou <c>apontamento.editar_todos</c>.
/// </summary>
public sealed class ComandosApontamento
{
    private readonly ITenantContext _contexto;
    private readonly IRepositorio<Apontamento> _apontamentos;
    private readonly IRepositorio<Cronometro> _cronometros;
    private readonly IRepositorio<Tarefa> _tarefas;
    private readonly IRepositorio<Solicitacao> _solicitacoes;
    private readonly IRepositorio<Projeto> _projetos;
    private readonly IRepositorio<RecorrenciaTarefa> _recorrencias;
    private readonly IRepositorio<Contrato> _contratos;
    private readonly IConsultasApontamento _consultas;
    private readonly RelogioDoTenant _relogio;
    private readonly IUnitOfWork _unidade;
    private readonly IControleVersao _versoes;
    private readonly IAuditoria _auditoria;

    public ComandosApontamento(
        ITenantContext contexto,
        IRepositorio<Apontamento> apontamentos,
        IRepositorio<Cronometro> cronometros,
        IRepositorio<Tarefa> tarefas,
        IRepositorio<Solicitacao> solicitacoes,
        IRepositorio<Projeto> projetos,
        IRepositorio<RecorrenciaTarefa> recorrencias,
        IRepositorio<Contrato> contratos,
        IConsultasApontamento consultas,
        RelogioDoTenant relogio,
        IUnitOfWork unidade,
        IControleVersao versoes,
        IAuditoria auditoria)
    {
        ArgumentNullException.ThrowIfNull(auditoria);
        _auditoria = auditoria;
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(apontamentos);
        ArgumentNullException.ThrowIfNull(cronometros);
        ArgumentNullException.ThrowIfNull(tarefas);
        ArgumentNullException.ThrowIfNull(solicitacoes);
        ArgumentNullException.ThrowIfNull(projetos);
        ArgumentNullException.ThrowIfNull(recorrencias);
        ArgumentNullException.ThrowIfNull(contratos);
        ArgumentNullException.ThrowIfNull(consultas);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(unidade);
        ArgumentNullException.ThrowIfNull(versoes);
        _contexto = contexto;
        _apontamentos = apontamentos;
        _cronometros = cronometros;
        _tarefas = tarefas;
        _solicitacoes = solicitacoes;
        _projetos = projetos;
        _recorrencias = recorrencias;
        _contratos = contratos;
        _consultas = consultas;
        _relogio = relogio;
        _unidade = unidade;
        _versoes = versoes;
    }

    public async Task<Guid> RegistrarAsync(NovoApontamentoRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.ApontamentoCriar);
        var tarefa = await _tarefas.ObterPorIdAsync(dados.TarefaId, cancellationToken)
            ?? throw ErroDeCampo.Criar("tarefaId", "Tarefa não encontrada.");
        var apontamento = await NovoAsync(
            tarefa, dados.Data, Duracao(dados.DuracaoMinutos), dados.Descricao, dados.DescricaoExterna, dados.Faturavel,
            dados.VisivelCliente, cancellationToken);
        await _unidade.SalvarAsync(cancellationToken);
        return apontamento.Id;
    }

    public async Task EditarAsync(Guid id, EdicaoApontamentoRequest dados, string? versao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var apontamento = await CarregarDoAutorAsync(id, versao, cancellationToken);
        var antes = Valores(apontamento);
        apontamento.Editar(
            dados.Data, Duracao(dados.DuracaoMinutos), dados.Descricao, dados.DescricaoExterna, dados.Faturavel, dados.VisivelCliente,
            await FechadosAsync(apontamento.ContratoId, cancellationToken));
        _auditoria.Registrar(AcoesAuditoria.ApontamentoEdicao, "apontamento", apontamento.Id, new { antes, depois = Valores(apontamento) });
        await _unidade.SalvarAsync(cancellationToken);
    }

    public async Task ExcluirAsync(Guid id, string? versao, CancellationToken cancellationToken = default)
    {
        var apontamento = await CarregarDoAutorAsync(id, versao, cancellationToken);
        apontamento.Excluir(await FechadosAsync(apontamento.ContratoId, cancellationToken));
        _apontamentos.Remover(apontamento);
        _auditoria.Registrar(AcoesAuditoria.ApontamentoExclusao, "apontamento", apontamento.Id, new { antes = Valores(apontamento) });
        await _unidade.SalvarAsync(cancellationToken);
    }

    /// <summary>Valor do apontamento para a auditoria (edição e exclusão guardam o anterior).</summary>
    private static object Valores(Apontamento apontamento) => new
    {
        apontamento.TarefaId,
        apontamento.UsuarioId,
        apontamento.ContratoId,
        apontamento.Data,
        duracaoMinutos = apontamento.Duracao.Minutos,
        apontamento.Descricao,
        apontamento.DescricaoExterna,
        apontamento.Faturavel,
        apontamento.VisivelCliente,
    };

    public async Task<Guid> IniciarCronometroAsync(InicioCronometroRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.ApontamentoCriar);
        var usuario = _contexto.Usuario();
        if (await _consultas.CronometroDoUsuarioAsync(usuario, cancellationToken) is not null)
            throw new CronometroAtivoException();
        var inicio = Inicio(dados.IniciadoEm);
        var tarefa = await _tarefas.ObterPorIdAsync(dados.TarefaId, cancellationToken)
            ?? throw ErroDeCampo.Criar("tarefaId", "Tarefa não encontrada.");
        // Começou a trabalhar (F11-19): a tarefa sai de A_FAZER; a solicitação dela sai de ABERTA (AndamentoPelaTarefa).
        if (tarefa.Status == StatusTarefa.AFazer)
            tarefa.Iniciar();

        var cronometro = Cronometro.Iniciar(
            Guid.CreateVersion7(), _contexto.TenantId!.Value, usuario, dados.TarefaId, inicio);
        _cronometros.Adicionar(cronometro);
        await _unidade.SalvarAsync(cancellationToken);
        return cronometro.Id;
    }

    /// <summary>Até quanto antes de agora o cronômetro pode começar (F12, D1).</summary>
    public static readonly TimeSpan InicioMaximoAtras = TimeSpan.FromHours(12);

    // Um minuto de folga para o relógio do aparelho um pouco adiantado.
    private DateTimeOffset Inicio(DateTimeOffset? informado)
    {
        var agora = _relogio.Agora;
        if (informado is not { } inicio)
            return agora;
        if (inicio > agora.AddMinutes(1))
            throw ErroDeCampo.Criar("iniciadoEm", "O início não pode ser no futuro.");
        if (inicio < agora - InicioMaximoAtras)
            throw ErroDeCampo.Criar("iniciadoEm", "O início pode ser de até 12 horas atrás.");
        return inicio > agora ? agora : inicio;
    }

    /// <summary>
    /// Para o cronômetro e cria o apontamento no dia do início (fuso do tenant, F12-07), com o tempo arredondado (modelo
    /// §3.4) ou a duração escolhida na janela (F12-05).
    /// </summary>
    public async Task<Guid> PararCronometroAsync(ParadaCronometroRequest? dados, CancellationToken cancellationToken = default)
    {
        var cronometro = await CronometroAtivoAsync(cancellationToken);
        var tarefa = await _tarefas.ObterPorIdAsync(cronometro.TarefaId, cancellationToken)
            ?? throw new NaoEncontradoException("A tarefa do cronômetro não existe mais.");
        var duracao = cronometro.Parar(_relogio.Agora);
        // A duração escolhida (F12-05) tem as regras do apontamento manual; o erro vem antes de remover o cronômetro.
        if (dados?.DuracaoMinutos is { } escolhida)
            duracao = Duracao(escolhida);

        var apontamento = await NovoAsync(
            tarefa,
            await _relogio.DiaAsync(cronometro.IniciadoEm, cancellationToken),
            duracao,
            string.IsNullOrWhiteSpace(dados?.Descricao) ? tarefa.Titulo : dados.Descricao,
            dados?.DescricaoExterna,
            dados?.Faturavel ?? true,
            dados?.VisivelCliente,
            cancellationToken);
        _cronometros.Remover(cronometro);
        await _unidade.SalvarAsync(cancellationToken);
        return apontamento.Id;
    }

    public async Task DescartarCronometroAsync(CancellationToken cancellationToken = default)
    {
        var cronometro = await CronometroAtivoAsync(cancellationToken);
        _cronometros.Remover(cronometro);
        await _unidade.SalvarAsync(cancellationToken);
    }

    private async Task<Apontamento> NovoAsync(
        Tarefa tarefa,
        DateOnly data,
        Duracao duracao,
        string descricao,
        string? descricaoExterna,
        bool faturavel,
        bool? visivelCliente,
        CancellationToken cancellationToken)
    {
        var contratoId = await ContratoValidoNaDataAsync(await ContratoDaTarefaAsync(tarefa, cancellationToken), data, cancellationToken);
        var apontamento = Apontamento.Registrar(
            Guid.CreateVersion7(),
            tarefa.TenantId,
            tarefa.Id,
            tarefa.ClienteId,
            _contexto.Usuario(),
            contratoId,
            data,
            duracao,
            descricao,
            descricaoExterna,
            faturavel,
            visivelCliente,
            await FechadosAsync(contratoId, cancellationToken));
        _apontamentos.Adicionar(apontamento);
        return apontamento;
    }

    private async Task<Cronometro> CronometroAtivoAsync(CancellationToken cancellationToken)
    {
        _contexto.Exigir(Permissoes.ApontamentoCriar);
        var id = await _consultas.CronometroDoUsuarioAsync(_contexto.Usuario(), cancellationToken)
            ?? throw new NaoEncontradoException("Nenhum cronômetro ativo.");
        return await _cronometros.ObterPorIdAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Nenhum cronômetro ativo.");
    }

    private async Task<Apontamento> CarregarDoAutorAsync(Guid id, string? versao, CancellationToken cancellationToken)
    {
        _contexto.Exigir(Permissoes.ApontamentoCriar);
        var apontamento = await _apontamentos.ObterPorIdAsync(id, cancellationToken)
            ?? throw new NaoEncontradoException("Apontamento não encontrado.");
        if (apontamento.UsuarioId != _contexto.UsuarioId && !_contexto.Permissoes.Contains(Permissoes.ApontamentoEditarTodos))
            throw new AcessoNegadoException("Só o autor ou quem pode editar todos os apontamentos altera este registro.");
        _versoes.Esperar(apontamento, versao);
        return apontamento;
    }

    /// <summary>
    /// RF-APT-03 com vigência: o apontamento consome o contrato da origem só se a data cai na vigência dele e ele não está
    /// SUSPENSO; fora disso entra como avulso. Encerrado conta para datas até o fim da vigência (horas lançadas depois do
    /// encerramento, mas referentes ao período do contrato).
    /// </summary>
    private async Task<Guid?> ContratoValidoNaDataAsync(Guid? contratoId, DateOnly data, CancellationToken cancellationToken)
    {
        if (contratoId is not { } id || await _contratos.ObterPorIdAsync(id, cancellationToken) is not { } contrato)
            return null;
        return contrato.Status != StatusContrato.Suspenso && contrato.Vigencia.Contem(data) ? id : null;
    }

    /// <summary>Contrato da origem da tarefa: solicitação, senão projeto, senão recorrência.</summary>
    private async Task<Guid?> ContratoDaTarefaAsync(Tarefa tarefa, CancellationToken cancellationToken)
    {
        if (tarefa.SolicitacaoId is { } solicitacaoId)
            return (await _solicitacoes.ObterPorIdAsync(solicitacaoId, cancellationToken))?.ContratoId;
        if (tarefa.ProjetoId is { } projetoId)
            return (await _projetos.ObterPorIdAsync(projetoId, cancellationToken))?.ContratoId;
        if (tarefa.RecorrenciaId is { } recorrenciaId)
            return (await _recorrencias.ObterPorIdAsync(recorrenciaId, cancellationToken))?.ContratoId;
        return null;
    }

    private async Task<IReadOnlyCollection<MesReferencia>> FechadosAsync(Guid? contratoId, CancellationToken cancellationToken) =>
        contratoId is { } id && await _contratos.ObterPorIdAsync(id, cancellationToken) is { } contrato
            ? contrato.PeriodosFechados
            : [];

    private static Duracao Duracao(int minutos) => minutos switch
    {
        <= 0 => throw ErroDeCampo.Criar("duracaoMinutos", "A duração precisa ser maior que zero."),
        > 24 * 60 => throw ErroDeCampo.Criar("duracaoMinutos", "A duração máxima de um apontamento é 24h."),
        _ => Domain.ValueObjects.Duracao.FromMinutos(minutos),
    };
}
