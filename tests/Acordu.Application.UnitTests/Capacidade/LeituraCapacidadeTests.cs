using Acordu.Application.Abstractions;
using Acordu.Application.Capacidade;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.UnitTests.Capacidade;

/// <summary>Contrato §11c: semana de segunda a domingo, soma por pessoa e por dia, sem estimativa e sem responsável.</summary>
public sealed class LeituraCapacidadeTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    // Quarta, 14/10/2026, 12:00 em Brasília.
    private static readonly DateTimeOffset Agora = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);
    private static readonly Referencia Ana = new(Guid.NewGuid(), "Ana Lima");
    private static readonly Referencia Rafael = new(Guid.NewGuid(), "Rafael Moura");

    private readonly ConsultasFalsas _consultas = new();

    private LeituraCapacidade Leitura(params string[] permissoes) =>
        new(new Contexto(permissoes.Length == 0 ? [Permissoes.TarefaVer, Permissoes.SolicitacaoVer] : permissoes), _consultas,
            new Calendarios(), new RelogioDoTenant(new Calendarios(), new Relogio()));

    [Fact]
    public async Task SemanaDeHoje_SomaPorPessoaEPorDia()
    {
        _consultas.Equipe.AddRange([new MembroCapacidade(Ana, Duracao.FromHoras(40)), new MembroCapacidade(Rafael, Duracao.FromHoras(20))]);
        _consultas.Tarefas.AddRange(
        [
            new TarefaCapacidade(Ana.Id, new DateOnly(2026, 10, 14), 240),
            new TarefaCapacidade(Ana.Id, new DateOnly(2026, 10, 14), 120),
            new TarefaCapacidade(Ana.Id, new DateOnly(2026, 10, 16), null),
            new TarefaCapacidade(Rafael.Id, new DateOnly(2026, 10, 13), 1500),
            new TarefaCapacidade(null, new DateOnly(2026, 10, 15), 60),
        ]);
        _consultas.Solicitacoes.AddRange([Ana.Id, Ana.Id, null]);

        var semana = await Leitura().ObterAsync(null);

        semana.Semana.Should().Be(new DateOnly(2026, 10, 12));
        semana.Ate.Should().Be(new DateOnly(2026, 10, 18));
        _consultas.Periodo.Should().Be((new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18)));
        var ana = semana.Pessoas.Single(p => p.Pessoa == Ana);
        ana.CapacidadeMinutos.Should().Be(2400);
        ana.Carga.PlanejadoMinutos.Should().Be(360);
        ana.Carga.Tarefas.Should().Be(3);
        ana.Carga.TarefasSemEstimativa.Should().Be(1);
        ana.Carga.Solicitacoes.Should().Be(2);
        ana.Percentual.Should().Be(15);
        ana.Carga.PorDia.Should().HaveCount(7);
        ana.Carga.PorDia.Single(d => d.Data == new DateOnly(2026, 10, 14)).PlanejadoMinutos.Should().Be(360);
        semana.Pessoas.Single(p => p.Pessoa == Rafael).Percentual.Should().Be(125);
        semana.SemResponsavel.PlanejadoMinutos.Should().Be(60);
        semana.SemResponsavel.Solicitacoes.Should().Be(1);
    }

    [Fact]
    public async Task QualquerDataDaSemana_NormalizaParaSegunda()
    {
        var semana = await Leitura().ObterAsync(new DateOnly(2026, 10, 25));

        semana.Semana.Should().Be(new DateOnly(2026, 10, 19));
        semana.Ate.Should().Be(new DateOnly(2026, 10, 25));
    }

    [Fact]
    public async Task CapacidadeZero_PercentualNulo_ESemCapacidadeUsaOPadrao()
    {
        _consultas.Equipe.AddRange([new MembroCapacidade(Ana, Duracao.Zero), new MembroCapacidade(Rafael, null)]);

        var semana = await Leitura().ObterAsync(null);

        semana.Pessoas.Single(p => p.Pessoa == Ana).Percentual.Should().BeNull();
        semana.Pessoas.Single(p => p.Pessoa == Rafael).CapacidadeMinutos.Should().Be(2400);
    }

    [Fact]
    public async Task ResponsavelForaDaEquipe_ContaComoSemResponsavel()
    {
        _consultas.Equipe.Add(new MembroCapacidade(Ana, Duracao.FromHoras(40)));
        _consultas.Tarefas.Add(new TarefaCapacidade(Guid.NewGuid(), new DateOnly(2026, 10, 14), 90));

        var semana = await Leitura().ObterAsync(null);

        semana.SemResponsavel.PlanejadoMinutos.Should().Be(90);
    }

    [Fact]
    public async Task SemSolicitacaoVer_SolicitacoesNulas()
    {
        _consultas.Equipe.Add(new MembroCapacidade(Ana, Duracao.FromHoras(40)));
        _consultas.Solicitacoes.Add(Ana.Id);

        var semana = await Leitura(Permissoes.TarefaVer).ObterAsync(null);

        semana.Pessoas[0].Carga.Solicitacoes.Should().BeNull();
        semana.SemResponsavel.Solicitacoes.Should().BeNull();
    }

    [Fact]
    public async Task SemTarefaVer_Nega()
    {
        var acao = () => Leitura(Permissoes.SolicitacaoVer).ObterAsync(null);

        await acao.Should().ThrowAsync<AcessoNegadoException>();
    }

    private sealed class ConsultasFalsas : IConsultasCapacidade
    {
        public List<MembroCapacidade> Equipe { get; } = [];

        public List<TarefaCapacidade> Tarefas { get; } = [];

        public List<Guid?> Solicitacoes { get; } = [];

        public (DateOnly De, DateOnly Ate) Periodo { get; private set; }

        public Task<IReadOnlyList<MembroCapacidade>> EquipeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MembroCapacidade>>(Equipe);

        public Task<IReadOnlyList<TarefaCapacidade>> TarefasAsync(DateOnly de, DateOnly ate, CancellationToken cancellationToken = default)
        {
            Periodo = (de, ate);
            return Task.FromResult<IReadOnlyList<TarefaCapacidade>>(Tarefas);
        }

        public Task<IReadOnlyList<Guid?>> SolicitacoesAsync(DateTimeOffset desde, DateTimeOffset antes, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid?>>(Solicitacoes);
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
