using Acordu.Domain.Entities;
using Acordu.Domain.ValueObjects;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Persistence;
using Acordu.Infrastructure.Persistence.Consultas;

namespace Acordu.Infrastructure.IntegrationTests;

[Collection(ColecaoBanco.Nome)]
public sealed class ConsultasProjetoESqlTests(BancoFixture banco)
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _cliente = Guid.NewGuid();

    private Tarefa TarefaDoProjeto(Projeto projeto, int? estimativaMinutos) =>
        Tarefa.Criar(
            Guid.NewGuid(), _tenant, "Tarefa", null, _cliente, null, projeto.Id, null, null,
            estimativaMinutos is { } m ? Duracao.FromMinutos(m) : null, false);

    private Apontamento Apontar(Tarefa tarefa, int minutos, bool faturavel) =>
        Apontamento.Registrar(
            Guid.NewGuid(), _tenant, tarefa.Id, _cliente, Guid.NewGuid(), null, Dados.Hoje, Duracao.FromMinutos(minutos),
            "Trabalho", null, faturavel, null, []);

    private async Task<Projeto> ProjetoAsync(Duracao? estimativa)
    {
        var projeto = Projeto.Criar(Guid.NewGuid(), _tenant, _cliente, "Site", null, null, estimativa);
        var concluida = TarefaDoProjeto(projeto, 120);
        concluida.Iniciar();
        concluida.Concluir(DateTimeOffset.UtcNow);
        var aFazer = TarefaDoProjeto(projeto, 60);
        var cancelada = TarefaDoProjeto(projeto, 500);
        cancelada.Cancelar();
        var deOutroProjeto = Tarefa.Criar(Guid.NewGuid(), _tenant, "Outra", null, _cliente, null, Guid.NewGuid(), null, null, null, false);

        await using var db = banco.DaEquipe(_tenant);
        db.AddRange(projeto, concluida, aFazer, cancelada, deOutroProjeto);
        db.AddRange(Apontar(concluida, 90, true), Apontar(aFazer, 30, false), Apontar(deOutroProjeto, 999, true));
        await db.SaveChangesAsync();
        return projeto;
    }

    private static ConsultasProjeto Consultas(AcorduDbContext db, Quem quem) => new(db, new ConsultaSql(db, quem));

    [Fact]
    public async Task Progresso_ContaSemCanceladasESomaTodoApontamentoDoProjeto()
    {
        var projeto = await ProjetoAsync(Duracao.FromHoras(40));

        await using var db = banco.DaEquipe(_tenant);
        var progresso = await Consultas(db, Quem.Equipe(_tenant)).ProgressoAsync(projeto.Id);

        progresso.Should().Be(new Application.Projetos.ProgressoProjetoDto(1, 2, 120, 2400));
    }

    [Fact]
    public async Task Progresso_SemEstimativaDoProjeto_SomaAsDasTarefasNaoCanceladas()
    {
        var projeto = await ProjetoAsync(null);

        await using var db = banco.DaEquipe(_tenant);
        (await Consultas(db, Quem.Equipe(_tenant)).ProgressoAsync(projeto.Id))!.EstimativaMinutos.Should().Be(180);
    }

    [Fact]
    public async Task Progresso_ContatoDoCliente_VeOMesmo()
    {
        var projeto = await ProjetoAsync(null);

        await using var db = banco.DoCliente(_tenant, _cliente);
        (await Consultas(db, Quem.DoCliente(_tenant, _cliente)).ProgressoAsync(projeto.Id))!.TarefasTotal.Should().Be(2);
    }

    [Fact]
    public async Task ConsultaSql_SemTenantNosParametros_Recusa()
    {
        await using var db = banco.DaEquipe(_tenant);
        var sql = new ConsultaSql(db, Quem.Equipe(_tenant));

        var acao = () => sql.ListarAsync<Contagem>(_ => $"SELECT count(*)::int AS total FROM tarefas");

        await acao.Should().ThrowAsync<IsolamentoVioladoException>();
    }

    [Fact]
    public async Task ConsultaSql_ContatoSemClienteNosParametros_Recusa()
    {
        await using var db = banco.DoCliente(_tenant, _cliente);
        var sql = new ConsultaSql(db, Quem.DoCliente(_tenant, _cliente));

        var acao = () => sql.ListarAsync<Contagem>(e => $"SELECT count(*)::int AS total FROM tarefas WHERE tenant_id = {e.TenantId}");

        await acao.Should().ThrowAsync<IsolamentoVioladoException>();
    }

    [Fact]
    public async Task ConsultaSql_SemTenantNoContexto_Recusa()
    {
        await using var db = banco.Contexto(Quem.Sistema());
        var sql = new ConsultaSql(db, Quem.Sistema());

        var acao = () => sql.ListarAsync<Contagem>(e => $"SELECT count(*)::int AS total FROM tarefas WHERE tenant_id = {e.TenantId}");

        await acao.Should().ThrowAsync<IsolamentoVioladoException>();
    }

    [Fact]
    public async Task ConsultaSql_ComEscopo_Executa()
    {
        await ProjetoAsync(null);
        await using var db = banco.DaEquipe(_tenant);
        var sql = new ConsultaSql(db, Quem.Equipe(_tenant));

        var linhas = await sql.ListarAsync<Contagem>(e => $"SELECT count(*)::int AS total FROM tarefas WHERE tenant_id = {e.TenantId}");

        linhas.Single().Total.Should().Be(4);
    }

    private sealed class Contagem
    {
        public int Total { get; set; }
    }
}
