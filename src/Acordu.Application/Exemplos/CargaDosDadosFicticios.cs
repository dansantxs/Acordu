using Acordu.Application.Abstractions;
using Acordu.Application.Anexos;
using Acordu.Application.Common;
using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Exemplos;

/// <summary>
/// Monta no tenant do contexto o que é ligado a cliente nos dados fictícios (<see cref="DadosFicticios"/>): clientes com
/// o contato e o contrato, projetos, solicitações com os passos, tarefas e apontamentos (RF-EMP-05; design do F14, D13).
/// Só adiciona às unidades de trabalho: quem chama confere as regras e grava. Categorias, equipe, recorrências,
/// cronômetro e portal ficam de fora (os exemplos são só o que é ligado a cliente; a demonstração do F14s soma o resto).
/// <para>
/// Nenhum efeito fora da transação: os eventos de domínio das raízes criadas são descartados antes da gravação, então
/// nada vai à outbox (nenhum e-mail, aviso no sino, seguimento ou feed). O efeito que os manipuladores da transação teriam
/// (primeira resposta, solicitação que sai de ABERTA pela tarefa) é aplicado aqui, como no caminho da API.
/// </para>
/// </summary>
public sealed class CargaDosDadosFicticios
{
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<Contrato> _contratos;
    private readonly IRepositorio<Solicitacao> _solicitacoes;
    private readonly IRepositorio<Interacao> _interacoes;
    private readonly IRepositorio<Projeto> _projetos;
    private readonly IRepositorio<Tarefa> _tarefas;
    private readonly IRepositorio<Apontamento> _apontamentos;
    private readonly IGeradorProtocolo _protocolo;
    private readonly ICalendarios _calendarios;
    private readonly RelogioDoTenant _relogio;

    public CargaDosDadosFicticios(
        IRepositorio<Cliente> clientes,
        IRepositorio<Contrato> contratos,
        IRepositorio<Solicitacao> solicitacoes,
        IRepositorio<Interacao> interacoes,
        IRepositorio<Projeto> projetos,
        IRepositorio<Tarefa> tarefas,
        IRepositorio<Apontamento> apontamentos,
        IGeradorProtocolo protocolo,
        ICalendarios calendarios,
        RelogioDoTenant relogio)
    {
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(contratos);
        ArgumentNullException.ThrowIfNull(solicitacoes);
        ArgumentNullException.ThrowIfNull(interacoes);
        ArgumentNullException.ThrowIfNull(projetos);
        ArgumentNullException.ThrowIfNull(tarefas);
        ArgumentNullException.ThrowIfNull(apontamentos);
        ArgumentNullException.ThrowIfNull(protocolo);
        ArgumentNullException.ThrowIfNull(calendarios);
        ArgumentNullException.ThrowIfNull(relogio);
        _clientes = clientes;
        _contratos = contratos;
        _solicitacoes = solicitacoes;
        _interacoes = interacoes;
        _projetos = projetos;
        _tarefas = tarefas;
        _apontamentos = apontamentos;
        _protocolo = protocolo;
        _calendarios = calendarios;
        _relogio = relogio;
    }

    /// <summary>
    /// Adiciona tudo ao tenant (sem gravar) e devolve quantos clientes criou.
    /// </summary>
    /// <param name="autor">Quem abre as solicitações e, sem <paramref name="pessoas"/>, faz tudo o mais.</param>
    /// <param name="pessoas">
    /// Usuário de cada chave de pessoa (a demonstração tem a equipe fictícia); chave ausente, ou nulo, é o
    /// <paramref name="autor"/> (nos exemplos, o Dono que carregou).
    /// </param>
    /// <param name="exemplo">Marca os clientes como exemplo (<see cref="Cliente.Exemplo"/>).</param>
    public async Task<int> MontarAsync(
        DadosFicticios dados,
        Tenant tenant,
        Guid autor,
        IReadOnlyDictionary<string, Guid>? pessoas,
        bool exemplo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentNullException.ThrowIfNull(tenant);
        var montagem = new Montagem(
            dados,
            tenant,
            autor,
            pessoas,
            new DiasRelativos(await _relogio.HojeAsync(cancellationToken)),
            _relogio.Agora,
            await _calendarios.DoTenantAsync(cancellationToken));

        foreach (var cliente in dados.Clientes)
            montagem.Cliente(cliente, exemplo);
        foreach (var projeto in dados.Projetos)
            montagem.Projeto(projeto);
        foreach (var solicitacao in dados.Solicitacoes)
            montagem.Solicitacao(solicitacao, await _protocolo.ProximoNumeroSolicitacaoAsync(cancellationToken));
        foreach (var tarefa in dados.Tarefas)
            montagem.Tarefa(tarefa);
        foreach (var apontamento in dados.Apontamentos)
            montagem.Apontamento(apontamento);
        // Status por último, como no bot do manual: primeiro as horas, depois o andamento.
        foreach (var tarefa in dados.Tarefas)
            montagem.StatusDaTarefa(tarefa);

        foreach (var raiz in montagem.Criadas)
        {
            raiz.LimparEventos();
            switch (raiz)
            {
                case Cliente c:
                    _clientes.Adicionar(c);
                    break;
                case Contrato c:
                    _contratos.Adicionar(c);
                    break;
                case Solicitacao s:
                    _solicitacoes.Adicionar(s);
                    break;
                case Interacao i:
                    _interacoes.Adicionar(i);
                    break;
                case Projeto p:
                    _projetos.Adicionar(p);
                    break;
                case Tarefa t:
                    _tarefas.Adicionar(t);
                    break;
                case Apontamento a:
                    _apontamentos.Adicionar(a);
                    break;
            }
        }

        return dados.Clientes.Count;
    }

    /// <summary>O estado de uma montagem: as raízes criadas e as chaves já resolvidas.</summary>
    private sealed class Montagem(
        DadosFicticios dados,
        Tenant tenant,
        Guid autor,
        IReadOnlyDictionary<string, Guid>? pessoas,
        DiasRelativos dias,
        DateTimeOffset agora,
        CalendarioUtil calendario)
    {
        private readonly Dictionary<string, (Cliente Cliente, Guid Contato, Contrato Contrato)> _clientes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Projeto> _projetos = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Solicitacao> _solicitacoes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (Tarefa Tarefa, Contrato? Contrato)> _tarefas = new(StringComparer.Ordinal);
        private readonly Dictionary<Guid, Contrato> _contratosPorId = [];

        public List<EntidadeBase> Criadas { get; } = [];

        public void Cliente(ClienteFicticio dado, bool exemplo)
        {
            var cliente = Domain.Entities.Cliente.Criar(Guid.CreateVersion7(), tenant.Id, dado.Nome, null, dado.Observacoes);
            var endereco = dado.Endereco is { } e
                ? new Endereco(e.Cep, e.Logradouro, e.Numero, e.Complemento, e.Bairro, e.Cidade, e.Uf)
                : null;
            cliente.DefinirCadastro(dado.RazaoSocial, null, dado.Email, dado.Telefone, endereco);
            cliente.DefinirEtiquetas(dado.Etiquetas);
            cliente.DefinirResponsavel(Pessoa(dado.Responsavel));
            if (exemplo)
                cliente.MarcarComoExemplo();
            var contato = cliente.AdicionarContato(dado.Contato.Nome, dado.Contato.Email, null, null);

            var termos = dado.Contrato;
            var contrato = Domain.Entities.Contrato.Criar(
                Guid.CreateVersion7(),
                tenant.Id,
                cliente.Id,
                termos.Tipo,
                new Periodo(dias.InicioDoMes(termos.InicioMeses)),
                termos.FranquiaMinutos is { } franquia ? Duracao.FromMinutos(franquia) : null,
                tenant.SlaSugerido);

            _clientes.Add(dado.Chave, (cliente, contato.Id, contrato));
            _contratosPorId.Add(contrato.Id, contrato);
            Criadas.Add(cliente);
            Criadas.Add(contrato);
        }

        public void Projeto(ProjetoFicticio dado)
        {
            var (cliente, _, contrato) = _clientes[dado.Cliente];
            var projeto = Domain.Entities.Projeto.Criar(
                Guid.CreateVersion7(),
                tenant.Id,
                cliente.Id,
                dado.Nome,
                contrato.Id,
                dias.Util(dado.Prazo),
                Minutos(dado.EstimativaMinutos),
                dias.Util(dado.Inicio));
            _projetos.Add(dado.Chave, projeto);
            Criadas.Add(projeto);
        }

        public void Solicitacao(SolicitacaoFicticia dado, int numero)
        {
            var (cliente, contato, contrato) = _clientes[dado.Cliente];
            // Categorias não entram nos exemplos (não são ligadas a cliente): a solicitação fica sem categoria.
            var solicitacao = Domain.Entities.Solicitacao.Abrir(
                Guid.CreateVersion7(),
                tenant.Id,
                cliente.Id,
                numero,
                dado.Titulo,
                dado.Descricao,
                dado.Prioridade,
                OrigemSolicitacao.Interna,
                contato,
                null,
                Pessoa(dado.Responsavel),
                contrato.Id,
                contrato.Sla,
                agora,
                calendario,
                autor);
            _solicitacoes.Add(dado.Chave, solicitacao);
            Criadas.Add(solicitacao);

            foreach (var passo in dado.Passos ?? [])
                Passo(solicitacao, passo);
        }

        public void Tarefa(TarefaFicticia dado)
        {
            Guid clienteId;
            Solicitacao? solicitacao = null;
            Projeto? projeto = null;
            Contrato? contrato = null;
            if (dado.Solicitacao is { } chaveSolicitacao)
            {
                solicitacao = _solicitacoes[chaveSolicitacao];
                clienteId = solicitacao.ClienteId;
                contrato = solicitacao.ContratoId is { } id ? _contratosPorId[id] : null;
            }
            else if (dado.Projeto is { } chaveProjeto)
            {
                projeto = _projetos[chaveProjeto];
                clienteId = projeto.ClienteId;
                contrato = projeto.ContratoId is { } id ? _contratosPorId[id] : null;
            }
            else
            {
                clienteId = _clientes[dado.Cliente ?? throw new InvalidOperationException($"A tarefa {dado.Chave} não tem cliente.")].Cliente.Id;
            }

            var dia = dias.Util(dado.Dia);
            var tarefa = Domain.Entities.Tarefa.Criar(
                Guid.CreateVersion7(),
                tenant.Id,
                dado.Titulo,
                null,
                clienteId,
                solicitacao?.Id,
                projeto?.Id,
                Pessoa(dado.Responsavel),
                dia,
                Minutos(dado.EstimativaMinutos),
                visivelCliente: projeto is not null,
                planejadaPara: dia);
            foreach (var item in dado.Checklist ?? [])
            {
                var novo = tarefa.AdicionarItem(item.Texto);
                if (item.Feito)
                    tarefa.MarcarItem(novo.Id, true);
            }

            // Tarefa criada na solicitação ABERTA: o trabalho começou (AndamentoPelaTarefa, F11-18).
            Andamento(solicitacao);
            _tarefas.Add(dado.Chave, (tarefa, contrato));
            Criadas.Add(tarefa);
        }

        public void Apontamento(ApontamentoFicticio dado)
        {
            if (dias.DoApontamento(dado) is not { } data)
                return;
            var (tarefa, contrato) = _tarefas[dado.Tarefa];
            // O contrato da origem só conta se a data cai na vigência (RF-APT-03); senão, avulso.
            var contratoId = contrato is not null && contrato.Vigencia.Contem(data) ? contrato.Id : (Guid?)null;
            Criadas.Add(Domain.Entities.Apontamento.Registrar(
                Guid.CreateVersion7(),
                tenant.Id,
                tarefa.Id,
                tarefa.ClienteId,
                Pessoa(dado.Por) ?? autor,
                contratoId,
                data,
                Duracao.FromMinutos(dado.Minutos),
                dado.Descricao,
                null,
                faturavel: true,
                visivelCliente: null,
                periodosFechados: []));
        }

        public void StatusDaTarefa(TarefaFicticia dado)
        {
            var (tarefa, _) = _tarefas[dado.Chave];
            if (dado.Status is not (StatusTarefa.Fazendo or StatusTarefa.Concluida))
                return;
            tarefa.Iniciar();
            Andamento(tarefa.SolicitacaoId is { } id ? _solicitacoes.Values.First(s => s.Id == id) : null);
            if (dado.Status == StatusTarefa.Concluida)
                tarefa.Concluir(agora);
        }

        private void Passo(Solicitacao solicitacao, PassoFicticio passo)
        {
            switch (passo.Acao)
            {
                case PassoFicticio.Inicio:
                    solicitacao.Iniciar(agora);
                    break;
                case PassoFicticio.Interacao:
                    var visibilidade = passo.Visibilidade ?? Visibilidade.Interna;
                    solicitacao.GarantirConversaAberta(doCliente: false, visibilidade, comAnexos: false);
                    Criadas.Add(Domain.Entities.Interacao.Registrar(
                        Guid.CreateVersion7(),
                        tenant.Id,
                        solicitacao.ClienteId,
                        solicitacao.Id,
                        Pessoa(passo.Por) ?? autor,
                        autorEhCliente: false,
                        Mencoes(passo.Texto),
                        visibilidade,
                        [],
                        RecebimentoAnexos.Politica,
                        agora));
                    // Resposta externa da equipe (PrimeiraRespostaERetomada, modelo §3.1).
                    if (visibilidade == Visibilidade.Externa)
                    {
                        solicitacao.RegistrarPrimeiraResposta(agora);
                        Andamento(solicitacao);
                    }

                    break;
                case PassoFicticio.AguardandoCliente:
                    solicitacao.AguardarCliente(agora);
                    break;
                case PassoFicticio.Resolucao:
                    var solucao = passo.Solucao
                        ?? throw new InvalidOperationException("O passo de resolução precisa da solução.");
                    solicitacao.Resolver(new Solucao(solucao.Texto, solucao.Problema, solucao.Causa), agora, calendario);
                    break;
                default:
                    throw new InvalidOperationException($"Passo desconhecido nos dados fictícios: {passo.Acao}.");
            }
        }

        private void Andamento(Solicitacao? solicitacao)
        {
            if (solicitacao?.Status == StatusSolicitacao.Aberta)
                solicitacao.Iniciar(agora);
        }

        /// <summary><c>@{chave}</c> vira a menção da pessoa (<c>@{id}</c>), como o envio da tela faz.</summary>
        private string? Mencoes(string? texto)
        {
            if (texto is null)
                return null;
            foreach (var pessoa in dados.Pessoas)
                texto = texto.Replace($"@{{{pessoa.Chave}}}", $"@{{{Pessoa(pessoa.Chave)}}}", StringComparison.Ordinal);
            return texto;
        }

        private Guid? Pessoa(string? chave) =>
            chave is null ? null : pessoas is not null && pessoas.TryGetValue(chave, out var id) ? id : autor;

        private static Duracao? Minutos(int? minutos) => minutos is { } m ? Duracao.FromMinutos(m) : null;
    }
}
