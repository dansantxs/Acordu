using Acordu.Application.Abstractions;
using Acordu.Application.Agenda;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.UnitTests.Agenda;

/// <summary>Read side §6a: período, atrasados, situações, recorrências expandidas e permissões por tipo.</summary>
public sealed class LeituraAgendaTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    // Quarta, 14/10/2026, 12:00 em Brasília.
    private static readonly DateTimeOffset Agora = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = new(2026, 10, 14);
    private static readonly Referencia Cliente = new(Guid.NewGuid(), "Padaria Bom Grão");
    private static readonly Referencia Ana = new(Guid.NewGuid(), "Ana Lima");

    private readonly ConsultasFalsas _consultas = new();

    private LeituraAgenda Leitura(params string[] permissoes) =>
        new(new Contexto(permissoes.Length == 0 ? [.. LeituraAgenda.PermissoesDeLeitura] : permissoes), _consultas, new Calendarios(),
            new RelogioDoTenant(new Calendarios(), new Relogio()));

    [Fact]
    public async Task PeriodoPadrao_DuasSemanasDaSegundaComDiasUteis()
    {
        var agenda = await Leitura().ObterAsync(null, null, null, null);

        agenda.De.Should().Be(new DateOnly(2026, 10, 12));
        agenda.Ate.Should().Be(new DateOnly(2026, 10, 25));
        agenda.Hoje.Should().Be(Hoje);
        agenda.Dias.Should().HaveCount(14);
        agenda.Dias.Single(d => d.Data == new DateOnly(2026, 10, 17)).Util.Should().BeFalse();
        agenda.Dias.Single(d => d.Data == Hoje).Util.Should().BeTrue();
        agenda.Equipe.Should().Equal(Ana);
    }

    [Fact]
    public async Task PeriodoAcimaDe62Dias_LancaErroDeCampo()
    {
        ArgumentException? erro = null;
        try
        {
            await Leitura().ObterAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 2), null, null);
        }
        catch (ArgumentException e)
        {
            erro = e;
        }

        erro.Should().NotBeNull();
        erro!.ParamName.Should().Be("ate");
    }

    [Fact]
    public async Task SemNenhumaPermissaoDeLeitura_Nega()
    {
        var acao = () => Leitura(Permissoes.DashboardVer).ObterAsync(null, null, null, null);

        await acao.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task Solicitacoes_SituacaoEAtrasadaVioladaSoComHoje()
    {
        var violada = Solicitacao(Agora.AddDays(-5), pausada: false);
        var pausada = Solicitacao(Agora.AddDays(-5), pausada: true);
        var emRisco = Solicitacao(Agora.AddHours(2), pausada: false, risco: Agora.AddHours(-1));
        var noPrazo = Solicitacao(Agora.AddDays(6), pausada: false);
        _consultas.Solicitacoes.AddRange([violada, pausada, emRisco, noPrazo]);

        var agenda = await Leitura().ObterAsync(null, null, null, null);
        var semHoje = await Leitura().ObterAsync(new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 30), null, null);

        Situacao(agenda, violada.Id).Should().Be(SituacaoItemAgenda.Violado);
        agenda.Itens.Single(i => i.Id == violada.Id).Inicio.Should().Be(new DateOnly(2026, 10, 9));
        agenda.Itens.Any(i => i.Id == pausada.Id).Should().BeFalse("pausada antes do período não é violação");
        Situacao(agenda, emRisco.Id).Should().Be(SituacaoItemAgenda.EmRisco);
        Situacao(agenda, noPrazo.Id).Should().Be(SituacaoItemAgenda.NoPrazo);
        semHoje.Itens.Select(i => i.Id).Should().Equal(noPrazo.Id);
    }

    [Fact]
    public async Task Tarefas_DiaPlanejadoPrimeiroEVencidaPeloPrazo()
    {
        var planejada = Tarefa(planejadaPara: new DateOnly(2026, 10, 15), prazo: new DateOnly(2026, 10, 20));
        var vencida = Tarefa(planejadaPara: null, prazo: new DateOnly(2026, 10, 13));
        var concluida = Tarefa(planejadaPara: null, prazo: new DateOnly(2026, 10, 13), StatusTarefa.Concluida);
        _consultas.Tarefas.AddRange([planejada, vencida, concluida]);

        var agenda = await Leitura().ObterAsync(null, null, null, null);

        var item = agenda.Itens.Single(i => i.Id == planejada.Id);
        item.Inicio.Should().Be(new DateOnly(2026, 10, 15));
        item.Prazo.Should().Be(new DateOnly(2026, 10, 20));
        item.Situacao.Should().Be(SituacaoItemAgenda.NoPrazo);
        Situacao(agenda, vencida.Id).Should().Be(SituacaoItemAgenda.Vencido);
        Situacao(agenda, concluida.Id).Should().Be(SituacaoItemAgenda.Concluido);
        _consultas.AtrasadasPedidas.Should().BeTrue();
    }

    [Fact]
    public async Task Projeto_BarraDoInicioAoPrazoESemInicioSoNoPrazo()
    {
        var barra = new ProjetoAgenda(Guid.NewGuid(), "Campanha", Cliente, new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 23));
        var marco = new ProjetoAgenda(Guid.NewGuid(), "Site", Cliente, null, new DateOnly(2026, 10, 20));
        _consultas.Projetos.AddRange([barra, marco]);

        var agenda = await Leitura().ObterAsync(null, null, null, null);

        var b = agenda.Itens.Single(i => i.Id == barra.Id);
        (b.Inicio, b.Fim).Should().Be((new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 23)));
        var m = agenda.Itens.Single(i => i.Id == marco.Id);
        (m.Inicio, m.Fim).Should().Be((new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 20)));
    }

    [Fact]
    public async Task Recorrencia_ExpandeOcorrenciasDesdeAProximaData()
    {
        var semanal = new RecorrenciaAgenda(
            Guid.NewGuid(), "Posts da semana", Cliente, Ana, 300, new RegraRecorrencia(Periodicidade.Semanal, 5, 2, new DateOnly(2026, 1, 1)),
            new DateOnly(2026, 10, 16));
        _consultas.Recorrencias.Add(semanal);

        var agenda = await Leitura().ObterAsync(null, null, null, null);

        agenda.Itens.Where(i => i.Tipo == TipoItemAgenda.Recorrencia).Select(i => i.Inicio)
            .Should().Equal(new DateOnly(2026, 10, 16), new DateOnly(2026, 10, 23));
        agenda.Itens.Should().OnlyContain(i => i.Situacao == SituacaoItemAgenda.Previsto && i.EstimativaMinutos == 300);
    }

    [Fact]
    public async Task Contrato_FimDaVigenciaEConsumoHoje()
    {
        var contrato = new ContratoAgenda(Guid.NewGuid(), Cliente, TipoContrato.BancoHoras, new DateOnly(2026, 10, 22), 86);
        _consultas.Contratos.Add(contrato);

        var agenda = await Leitura().ObterAsync(null, null, null, null);

        var marcos = agenda.Itens.Where(i => i.Id == contrato.Id).ToList();
        marcos.Should().HaveCount(2);
        marcos.Single(i => i.Marco == MarcoContrato.ConsumoAlto).Inicio.Should().Be(Hoje);
        marcos.Single(i => i.Marco == MarcoContrato.ConsumoAlto).Percentual.Should().Be(86);
        marcos.Single(i => i.Marco == MarcoContrato.FimVigencia).Inicio.Should().Be(new DateOnly(2026, 10, 22));
    }

    [Fact]
    public async Task SoTarefaVer_SoTarefas()
    {
        _consultas.Solicitacoes.Add(Solicitacao(Agora.AddDays(1), pausada: false));
        _consultas.Tarefas.Add(Tarefa(null, new DateOnly(2026, 10, 15)));
        _consultas.Contratos.Add(new ContratoAgenda(Guid.NewGuid(), Cliente, TipoContrato.BancoHoras, new DateOnly(2026, 10, 22), 90));

        var agenda = await Leitura(Permissoes.TarefaVer).ObterAsync(null, null, null, null);

        agenda.Itens.Should().HaveCount(1);
        agenda.Itens[0].Tipo.Should().Be(TipoItemAgenda.Tarefa);
    }

    [Fact]
    public async Task FiltroPorPessoa_TiraProjetosEContratosERecorrenciasDeOutros()
    {
        _consultas.Projetos.Add(new ProjetoAgenda(Guid.NewGuid(), "Campanha", Cliente, null, new DateOnly(2026, 10, 20)));
        _consultas.Contratos.Add(new ContratoAgenda(Guid.NewGuid(), Cliente, TipoContrato.BancoHoras, new DateOnly(2026, 10, 22), 90));
        _consultas.Recorrencias.Add(new RecorrenciaAgenda(
            Guid.NewGuid(), "Fechamento", Cliente, null, null, new RegraRecorrencia(Periodicidade.Mensal, 20, 0, new DateOnly(2026, 1, 1)),
            new DateOnly(2026, 10, 20)));

        var agenda = await Leitura().ObterAsync(null, null, null, Ana.Id);

        agenda.Itens.Should().BeEmpty();
    }

    private static SituacaoItemAgenda Situacao(AgendaDto agenda, Guid id) => agenda.Itens.Single(i => i.Id == id).Situacao;

    private static SolicitacaoAgenda Solicitacao(DateTimeOffset prazo, bool pausada, DateTimeOffset? risco = null) =>
        new(Guid.NewGuid(), 128, "Banner", Cliente, Ana, prazo, prazo.AddDays(-1), risco ?? prazo.AddHours(-4), pausada, Respondida: true);

    private static TarefaAgenda Tarefa(DateOnly? planejadaPara, DateOnly? prazo, StatusTarefa status = StatusTarefa.AFazer) =>
        new(Guid.NewGuid(), "Arte", status, Cliente, Ana, planejadaPara, prazo, 120, null, null);

    private sealed class ConsultasFalsas : IConsultasAgenda
    {
        public List<SolicitacaoAgenda> Solicitacoes { get; } = [];

        public List<TarefaAgenda> Tarefas { get; } = [];

        public List<ProjetoAgenda> Projetos { get; } = [];

        public List<RecorrenciaAgenda> Recorrencias { get; } = [];

        public List<ContratoAgenda> Contratos { get; } = [];

        public bool AtrasadasPedidas { get; private set; }

        public Task<IReadOnlyList<Referencia>> EquipeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Referencia>>([Ana]);

        public Task<IReadOnlyList<SolicitacaoAgenda>> SolicitacoesAsync(
            DateTimeOffset antes, FiltroAgenda filtro, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SolicitacaoAgenda>>([.. Solicitacoes.Where(s => s.PrazoResolucao < antes)]);

        public Task<IReadOnlyList<TarefaAgenda>> TarefasAsync(
            DateOnly de, DateOnly ate, bool atrasadas, FiltroAgenda filtro, CancellationToken cancellationToken = default)
        {
            AtrasadasPedidas = atrasadas;
            return Task.FromResult<IReadOnlyList<TarefaAgenda>>([.. Tarefas.Where(t => filtro.ResponsavelId is null || t.Responsavel?.Id == filtro.ResponsavelId)]);
        }

        public Task<IReadOnlyList<ProjetoAgenda>> ProjetosAsync(
            DateOnly de, DateOnly ate, bool atrasados, Guid? clienteId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjetoAgenda>>(Projetos);

        public Task<IReadOnlyList<RecorrenciaAgenda>> RecorrenciasAsync(DateOnly ate, Guid? clienteId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RecorrenciaAgenda>>(Recorrencias);

        public Task<IReadOnlyList<ContratoAgenda>> ContratosAsync(
            DateOnly de, DateOnly ate, DateOnly hoje, Guid? clienteId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContratoAgenda>>(Contratos);
    }

    private sealed class Contexto(IEnumerable<string> permissoes) : ITenantContext
    {
        public Guid? TenantId => Tenant;

        public bool EhCliente => false;

        public Guid? ClienteId => null;

        public Guid? UsuarioId => Ana.Id;

        public string? Papel => "Dono";

        public IReadOnlySet<string> Permissoes { get; } = permissoes.ToHashSet();

        public EscopoPortal? EscopoPortal => null;

        public Guid? AcessoPortalId => null;

        public Guid? SolicitacaoPermitida => null;
    }

    private sealed class Calendarios : ICalendarios
    {
        public Task<CalendarioUtil> DoTenantAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CalendarioUtil.Padrao(Tenant));

        public Task<CalendarioUtil> ParaEdicaoAsync(CancellationToken cancellationToken = default) => DoTenantAsync(cancellationToken);
    }

    private sealed class Relogio : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Agora;
    }
}
