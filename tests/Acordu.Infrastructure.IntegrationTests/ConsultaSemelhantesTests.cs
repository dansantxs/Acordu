using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;
using Acordu.Infrastructure.Persistence;
using Acordu.Infrastructure.Persistence.Consultas;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>Semelhantes (read side §8) e casos de borda do §11.</summary>
[Collection(ColecaoBanco.Nome)]
public sealed class ConsultaSemelhantesTests(BancoFixture banco)
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _cliente = Guid.NewGuid();
    private int _numero;

    private static ConsultaSemelhantes Consulta(AcorduDbContext db, Quem quem) => new(db, new ConsultaSql(db, quem));

    private Solicitacao Nova(string titulo, string? descricao = null, Guid? tenant = null, Guid? cliente = null, Guid? categoria = null)
    {
        var t = tenant ?? _tenant;
        return Solicitacao.Abrir(
            Guid.NewGuid(), t, cliente ?? _cliente, ++_numero, titulo, descricao, Prioridade.Media, OrigemSolicitacao.Interna,
            null, categoria, null, null, null, Dados.Agora, CalendarioUtil.Padrao(t));
    }

    private Solicitacao Resolvida(
        string titulo, string solucao, string? descricao = null, Guid? tenant = null, Guid? cliente = null, Guid? categoria = null)
    {
        var s = Nova(titulo, descricao, tenant, cliente, categoria);
        s.Resolver(new Solucao(solucao), Dados.Agora.AddHours(2), CalendarioUtil.Padrao(s.TenantId));
        return s;
    }

    private async Task GravarAsync(params Solicitacao[] solicitacoes)
    {
        foreach (var grupo in solicitacoes.GroupBy(s => s.TenantId))
        {
            await using var db = banco.DaEquipe(grupo.Key);
            db.AddRange(grupo);
            await db.SaveChangesAsync();
        }
    }

    private async Task<IReadOnlyList<Application.Solicitacoes.SolicitacaoSemelhanteDto>> SemelhantesAsync(Solicitacao alvo)
    {
        await using var db = banco.DaEquipe(_tenant);
        return (await Consulta(db, Quem.Equipe(_tenant)).SemelhantesAsync(alvo.Id))!;
    }

    [Fact]
    public async Task FullText_IgnoraAcentoEOrdemDasPalavras()
    {
        var resolvida = Resolvida("Relatorio de vendas nao abre", "Reinstalado o leitor de PDF.");
        var alvo = Nova("Relatório não abre", "O relatório de vendas trava ao abrir.");
        await GravarAsync(resolvida, alvo);

        var semelhantes = await SemelhantesAsync(alvo);

        semelhantes.Should().ContainSingle();
        semelhantes[0].Id.Should().Be(resolvida.Id);
        semelhantes[0].Numero.Should().Be(resolvida.Numero);
        semelhantes[0].SolucaoResumo.Should().Be("Reinstalado o leitor de PDF.");
        semelhantes[0].ResolvidaEm.Should().Be(resolvida.ResolvidaEm!.Value);
        semelhantes[0].Relevancia.Should().BeGreaterThanOrEqualTo(ConsultaSemelhantes.RelevanciaMinima);
    }

    [Fact]
    public async Task TituloComErroDeDigitacao_EncontradoPorTrigrama()
    {
        // Sem lexema em comum (impressor/trav × impresor/travd): só o trigrama do título encontra.
        var resolvida = Resolvida("Impressora travada", "Driver atualizado.");
        var alvo = Nova("Impresora travda");
        await GravarAsync(resolvida, alvo);

        (await SemelhantesAsync(alvo)).Select(s => s.Id).Should().Contain(resolvida.Id);
    }

    [Fact]
    public async Task SoResolvidasOuFechadas_ESemAPropria()
    {
        var aberta = Nova("Banner da campanha com data errada");
        var cancelada = Nova("Banner da campanha com data errada");
        cancelada.Cancelar("Duplicada", Dados.Agora);
        var fechada = Resolvida("Banner da campanha com data errada", "Arte refeita.");
        fechada.Fechar(Dados.Agora.AddDays(5));
        var alvo = Nova("Banner da campanha com data errada");
        await GravarAsync(aberta, cancelada, fechada, alvo);

        (await SemelhantesAsync(alvo)).Select(s => s.Id).Should().Equal(fechada.Id);
    }

    [Fact]
    public async Task OutroTenant_NuncaAparece()
    {
        var outroTenant = Guid.NewGuid();
        var deOutroTenant = Resolvida("Banner da campanha com data errada", "Arte refeita.", tenant: outroTenant);
        var alvo = Nova("Banner da campanha com data errada");
        await GravarAsync(deOutroTenant, alvo);

        (await SemelhantesAsync(alvo)).Should().BeEmpty();
    }

    [Fact]
    public async Task AssuntoSemRelacao_NaoAparece()
    {
        var semRelacao = Resolvida("Trocar senha do e-mail", "Senha redefinida no painel.");
        var alvo = Nova("Banner da campanha com data errada");
        await GravarAsync(semRelacao, alvo);

        (await SemelhantesAsync(alvo)).Should().BeEmpty();
    }

    [Fact]
    public async Task NoMaximoCinco_DaMaisRelevante()
    {
        var resolvidas = Enumerable.Range(1, 7)
            .Select(i => Resolvida($"Banner da campanha com data errada {i}", "Arte refeita."))
            .ToArray();
        var alvo = Nova("Banner da campanha com data errada");
        await GravarAsync([.. resolvidas, alvo]);

        var semelhantes = await SemelhantesAsync(alvo);

        semelhantes.Should().HaveCount(ConsultaSemelhantes.Maximo);
        semelhantes.Select(s => s.Relevancia).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task MesmoClienteEMesmaCategoria_DesempatamSemFiltrar()
    {
        var categoria = Guid.NewGuid();
        var deOutroCliente = Resolvida("Banner da campanha com data errada", "Arte refeita.", cliente: Guid.NewGuid());
        var doMesmoCliente = Resolvida("Banner da campanha com data errada", "Arte refeita.", categoria: categoria);
        var alvo = Nova("Banner da campanha com data errada", categoria: categoria);
        await GravarAsync(deOutroCliente, doMesmoCliente, alvo);

        var semelhantes = await SemelhantesAsync(alvo);

        semelhantes.Select(s => s.Id).Should().Equal(doMesmoCliente.Id, deOutroCliente.Id);
        (semelhantes[0].Relevancia - semelhantes[1].Relevancia).Should().BeApproximately(0.10, 0.011);
    }

    [Fact]
    public async Task Contato_SoVeSolucoesDoProprioCliente()
    {
        var deOutroCliente = Resolvida("Banner da campanha com data errada", "Arte refeita.", cliente: Guid.NewGuid());
        var doCliente = Resolvida("Banner da campanha com data errada", "Arte refeita.");
        var alvo = Nova("Banner da campanha com data errada");
        await GravarAsync(deOutroCliente, doCliente, alvo);

        await using var db = banco.DoCliente(_tenant, _cliente);
        var semelhantes = await Consulta(db, Quem.DoCliente(_tenant, _cliente)).SemelhantesAsync(alvo.Id);

        semelhantes!.Select(s => s.Id).Should().Equal(doCliente.Id);
    }

    [Fact]
    public async Task AlvoInvisivel_Nulo()
    {
        var alvo = Nova("Banner da campanha com data errada");
        await GravarAsync(alvo);
        var outroTenant = Guid.NewGuid();

        await using var db = banco.DaEquipe(outroTenant);
        (await Consulta(db, Quem.Equipe(outroTenant)).SemelhantesAsync(alvo.Id)).Should().BeNull();
    }
}
