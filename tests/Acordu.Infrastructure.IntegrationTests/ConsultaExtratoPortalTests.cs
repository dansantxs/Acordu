using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;
using Acordu.Infrastructure.Persistence;
using Acordu.Infrastructure.Persistence.Consultas;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>Extrato do portal (read side §4): só faturáveis, linha genérica para não visíveis, total conferível.</summary>
[Collection(ColecaoBanco.Nome)]
public sealed class ConsultaExtratoPortalTests(BancoFixture banco)
{
    private static readonly MesReferencia Outubro = new(2026, 10);

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _cliente = Guid.NewGuid();

    private static ConsultaExtratoPortal Consulta(AcorduDbContext db, Quem quem) => new(db, new ConsultaSql(db, quem));

    /// <summary>
    /// Contrato com: solicitação #N e tarefa dela; tarefa recorrente visível (sem solicitação) e tarefa interna não visível.
    /// Apontamentos: visível com descrição externa, visível sem externa, faturável não visível, não faturável e um de novembro.
    /// </summary>
    private async Task<(Contrato Contrato, Solicitacao Solicitacao)> CenarioAsync()
    {
        var contrato = Contrato.Criar(
            Guid.NewGuid(), _tenant, _cliente, TipoContrato.BancoHoras, new Periodo(new DateOnly(2026, 10, 1)),
            Duracao.FromHoras(20), Dados.Politica());
        var solicitacao = Dados.Solicitacao(_tenant, _cliente, contrato.Id);
        var tarefaDaSolicitacao = Dados.Tarefa(_tenant, _cliente, solicitacao.Id);
        var recorrente = Tarefa.Criar(
            Guid.NewGuid(), _tenant, "Fechamento mensal", null, _cliente, null, null, null, null, null, visivelCliente: true);
        var interna = Tarefa.Criar(
            Guid.NewGuid(), _tenant, "Revisão interna", null, _cliente, null, null, null, null, null, visivelCliente: false);

        await using var db = banco.DaEquipe(_tenant);
        db.AddRange(contrato, solicitacao, tarefaDaSolicitacao, recorrente, interna);
        db.AddRange(
            Apontar(tarefaDaSolicitacao, contrato, 5, 90, "Ajuste de layout", "Arte refeita", faturavel: true, visivel: true),
            Apontar(recorrente, contrato, 6, 60, "Conciliação", null, faturavel: true, visivel: true),
            Apontar(interna, contrato, 7, 45, "Revisão de código", null, faturavel: true, visivel: false),
            Apontar(tarefaDaSolicitacao, contrato, 8, 30, "Reunião interna", null, faturavel: false, visivel: false),
            Apontar(tarefaDaSolicitacao, contrato, 5, 15, "Publicação", null, faturavel: true, visivel: true, mes: 11));
        await db.SaveChangesAsync();
        return (contrato, solicitacao);
    }

    private Apontamento Apontar(
        Tarefa tarefa, Contrato contrato, int dia, int minutos, string descricao, string? externa, bool faturavel, bool visivel, int mes = 10) =>
        Apontamento.Registrar(
            Guid.NewGuid(), _tenant, tarefa.Id, _cliente, Guid.NewGuid(), contrato.Id, new DateOnly(2026, mes, dia),
            Duracao.FromMinutos(minutos), descricao, externa, faturavel, visivel, []);

    [Fact]
    public async Task Extrato_SoFaturaveisDoMes_ComLinhasConferiveis()
    {
        var (contrato, solicitacao) = await CenarioAsync();

        await using var db = banco.DoCliente(_tenant, _cliente);
        var extrato = await Consulta(db, Quem.DoCliente(_tenant, _cliente)).ExtratoAsync(contrato.Id, Outubro);

        extrato!.Linhas.Should().HaveCount(3);
        extrato.ConsumidoMinutos.Should().Be(90 + 60 + 45);
        extrato.ConsumidoMinutos.Should().Be(extrato.Linhas.Sum(l => l.DuracaoMinutos));

        extrato.Linhas[0].Should().Be(new Application.Portal.Extrato.LinhaExtratoDto(
            new DateOnly(2026, 10, 5), solicitacao.Numero, "Ajustar banner", "Arte refeita", 90));
        extrato.Linhas[1].Should().Be(new Application.Portal.Extrato.LinhaExtratoDto(
            new DateOnly(2026, 10, 6), null, "Fechamento mensal", "Conciliação", 60));
        extrato.Linhas[2].Should().Be(new Application.Portal.Extrato.LinhaExtratoDto(
            new DateOnly(2026, 10, 7), null, null, ConsultaExtratoPortal.DescricaoGenerica, 45));
    }

    [Fact]
    public async Task Extrato_DescricaoInternaNuncaApareceEmLinhaNaoVisivel()
    {
        var (contrato, _) = await CenarioAsync();

        await using var db = banco.DoCliente(_tenant, _cliente);
        var extrato = await Consulta(db, Quem.DoCliente(_tenant, _cliente)).ExtratoAsync(contrato.Id, Outubro);

        extrato!.Linhas.Select(l => l.Descricao).Should().NotContain(["Revisão de código", "Reunião interna"]);
    }

    [Fact]
    public async Task Extrato_ContatoDeOutroCliente_Nulo()
    {
        var (contrato, _) = await CenarioAsync();
        var outro = Guid.NewGuid();

        await using var db = banco.DoCliente(_tenant, outro);
        (await Consulta(db, Quem.DoCliente(_tenant, outro)).ExtratoAsync(contrato.Id, Outubro)).Should().BeNull();
    }

    [Fact]
    public async Task Extrato_Equipe_VeOMesmoExtrato()
    {
        var (contrato, _) = await CenarioAsync();

        await using var db = banco.DaEquipe(_tenant);
        var extrato = await Consulta(db, Quem.Equipe(_tenant)).ExtratoAsync(contrato.Id, Outubro);

        extrato!.ConsumidoMinutos.Should().Be(195);
    }
}
