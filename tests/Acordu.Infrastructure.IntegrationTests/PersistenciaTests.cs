using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>Cada agregado é gravado e relido igual, num contexto novo (sem cache do change tracker).</summary>
[Collection(ColecaoBanco.Nome)]
public sealed class PersistenciaTests(BancoFixture banco)
{
    private readonly Guid _tenant = Guid.NewGuid();

    private async Task Gravar(params object[] entidades)
    {
        await using var db = banco.DaEquipe(_tenant);
        db.AddRange(entidades);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Contrato_VigenciaFranquiaEPoliticaEmJson()
    {
        var cliente = Dados.Cliente(_tenant);
        var contrato = Dados.Contrato(_tenant, cliente.Id);
        await Gravar(cliente, contrato);

        await using var db = banco.DaEquipe(_tenant);
        var lido = await db.Contratos.SingleAsync(c => c.Id == contrato.Id);

        lido.Tipo.Should().Be(TipoContrato.BancoHoras);
        lido.Vigencia.Should().Be(new Periodo(new DateOnly(2026, 10, 1)));
        lido.Franquia.Should().Be(Duracao.FromHoras(20));
        lido.Sla.Should().Be(Dados.Politica());
        lido.Status.Should().Be(StatusContrato.Ativo);
    }

    [Fact]
    public async Task Enum_GravadoEmScreamingSnake_DuracaoEmMinutos()
    {
        var cliente = Dados.Cliente(_tenant);
        var contrato = Dados.Contrato(_tenant, cliente.Id);
        await Gravar(cliente, contrato);

        await using var db = banco.DaEquipe(_tenant);
        var linha = await db.Database
            .SqlQuery<LinhaContrato>($"SELECT tipo, franquia_minutos FROM contratos WHERE id = {contrato.Id}")
            .SingleAsync();

        linha.Tipo.Should().Be("BANCO_HORAS");
        linha.FranquiaMinutos.Should().Be(1200);
    }

    [Fact]
    public async Task AjusteSaldo_QuantidadeNegativa()
    {
        var cliente = Dados.Cliente(_tenant);
        var contrato = Dados.Contrato(_tenant, cliente.Id);
        var ajuste = AjusteSaldo.Registrar(Guid.NewGuid(), contrato, new DuracaoAssinada(-90), "Estorno", Dados.Hoje, Guid.NewGuid());
        await Gravar(cliente, contrato, ajuste);

        await using var db = banco.DaEquipe(_tenant);
        (await db.AjustesSaldo.SingleAsync(a => a.Id == ajuste.Id)).Quantidade.Should().Be(new DuracaoAssinada(-90));
    }

    [Fact]
    public async Task Solicitacao_ComRelogioPausadoESolucao()
    {
        var cliente = Dados.Cliente(_tenant);
        var contrato = Dados.Contrato(_tenant, cliente.Id);
        var solicitacao = Dados.Solicitacao(_tenant, cliente.Id, contrato.Id);
        solicitacao.AguardarCliente(Dados.Agora.AddHours(1));
        await Gravar(cliente, contrato, solicitacao);

        await using (var db = banco.DaEquipe(_tenant))
        {
            var lida = await db.Solicitacoes.SingleAsync(s => s.Id == solicitacao.Id);
            lida.Status.Should().Be(StatusSolicitacao.AguardandoCliente);
            lida.Sla.Should().Be(solicitacao.Sla);
            lida.Solucao.Should().BeNull();

            lida.Resolver(new Solucao("Arte refeita", "Data errada", null), Dados.Agora.AddHours(3), CalendarioUtil.Padrao(_tenant));
            await db.SaveChangesAsync();
        }

        await using var outro = banco.DaEquipe(_tenant);
        var resolvida = await outro.Solicitacoes.SingleAsync(s => s.Id == solicitacao.Id);
        resolvida.Status.Should().Be(StatusSolicitacao.Resolvida);
        resolvida.Solucao.Should().Be(new Solucao("Arte refeita", "Data errada", null));
        resolvida.Sla!.Pausado.Should().BeFalse();
        resolvida.Sla.PrazoResolucao.Should().Be(solicitacao.Sla!.PrazoResolucao.AddHours(2));
    }

    [Fact]
    public async Task Solicitacao_SemContrato_RelogioNulo()
    {
        var cliente = Dados.Cliente(_tenant);
        var solicitacao = Dados.Solicitacao(_tenant, cliente.Id);
        await Gravar(cliente, solicitacao);

        await using var db = banco.DaEquipe(_tenant);
        (await db.Solicitacoes.SingleAsync(s => s.Id == solicitacao.Id)).Sla.Should().BeNull();
    }

    [Fact]
    public async Task Tarefa_ChecklistMantemOrdemEAceitaItemNovo()
    {
        var tarefa = Dados.Tarefa(_tenant, null);
        tarefa.AdicionarItem("Um");
        tarefa.AdicionarItem("Dois");
        tarefa.AdicionarItem("Três");
        await Gravar(tarefa);

        await using (var db = banco.DaEquipe(_tenant))
        {
            var lida = await db.Tarefas.SingleAsync(t => t.Id == tarefa.Id);
            lida.RemoverItem(lida.Checklist[1].Id);
            lida.AdicionarItem("Quatro");
            lida.MarcarItem(lida.Checklist[0].Id, true);
            await db.SaveChangesAsync();
        }

        await using var outro = banco.DaEquipe(_tenant);
        var relida = await outro.Tarefas.SingleAsync(t => t.Id == tarefa.Id);
        relida.Checklist.Select(i => i.Texto).Should().Equal("Um", "Três", "Quatro");
        relida.Checklist[0].Feito.Should().BeTrue();
        relida.Estimativa.Should().Be(Duracao.FromHoras(2));
    }

    [Fact]
    public async Task Apontamento_EProjeto()
    {
        var cliente = Dados.Cliente(_tenant);
        var projeto = Dados.Projeto(_tenant, cliente.Id);
        var tarefa = Dados.Tarefa(_tenant, cliente.Id);
        var apontamento = Dados.Apontamento(_tenant, cliente.Id, tarefa.Id);
        await Gravar(cliente, projeto, tarefa, apontamento);

        await using var db = banco.DaEquipe(_tenant);
        var lido = await db.Apontamentos.SingleAsync(a => a.Id == apontamento.Id);
        lido.Duracao.Should().Be(Duracao.FromMinutos(90));
        lido.VisivelCliente.Should().BeTrue();
        (await db.Projetos.SingleAsync(p => p.Id == projeto.Id)).Estimativa.Should().Be(Duracao.FromHoras(40));
    }

    [Fact]
    public async Task Recorrencia_RegraEModelo()
    {
        var cliente = Dados.Cliente(_tenant);
        var recorrencia = Dados.Recorrencia(_tenant, cliente.Id);
        await Gravar(cliente, recorrencia);

        await using var db = banco.DaEquipe(_tenant);
        var lida = await db.Recorrencias.SingleAsync(r => r.Id == recorrencia.Id);
        lida.Regra.Should().Be(recorrencia.Regra);
        lida.Modelo.Should().Be(recorrencia.Modelo);
        lida.ProximaData.Should().Be(new DateOnly(2026, 10, 31));
    }

    [Fact]
    public async Task Cliente_ContatosEContatoNovo()
    {
        var cliente = Dados.Cliente(_tenant);
        cliente.AdicionarContato("Marina", "marina@bomgrao.com.br", null, "Sócia");
        await Gravar(cliente);

        await using (var db = banco.DaEquipe(_tenant))
        {
            var lido = await db.Clientes.SingleAsync(c => c.Id == cliente.Id);
            lido.AdicionarContato("José", "jose@bomgrao.com.br", null, null);
            await db.SaveChangesAsync();
        }

        await using var outro = banco.DaEquipe(_tenant);
        var relido = await outro.Clientes.SingleAsync(c => c.Id == cliente.Id);
        relido.Documento.Should().Be("11222333000181");
        relido.Contatos.Select(c => c.Email).Should().BeEquivalentTo(["marina@bomgrao.com.br", "jose@bomgrao.com.br"]);
    }

    [Fact]
    public async Task Interacao_ComAnexos()
    {
        var cliente = Dados.Cliente(_tenant);
        var solicitacao = Dados.Solicitacao(_tenant, cliente.Id);
        var interacao = Dados.Interacao(_tenant, cliente.Id, solicitacao.Id);
        await Gravar(cliente, solicitacao, interacao);

        await using var db = banco.DaEquipe(_tenant);
        var lida = await db.Interacoes.SingleAsync(i => i.Id == interacao.Id);
        lida.Visibilidade.Should().Be(Visibilidade.Externa);
        lida.Anexos.Should().ContainSingle();
        lida.Anexos[0].NomeArquivo.Should().Be("arte.png");
    }

    [Fact] // F15-60
    public async Task Interacao_CanalGravadoComoTextoERelido()
    {
        var cliente = Dados.Cliente(_tenant);
        var solicitacao = Dados.Solicitacao(_tenant, cliente.Id);
        var porEmail = Interacao.Registrar(
            Guid.NewGuid(), _tenant, cliente.Id, solicitacao.Id, Guid.NewGuid(), true, "Mandei por e-mail", Visibilidade.Externa,
            CanalMensagem.Email, [], new PoliticaAnexos(1024, ["image/png"], 5), Dados.Agora);
        await Gravar(cliente, solicitacao, porEmail);

        await using var db = banco.DaEquipe(_tenant);
        (await db.Interacoes.SingleAsync(i => i.Id == porEmail.Id)).Canal.Should().Be(CanalMensagem.Email);
        var gravado = await db.Database
            .SqlQuery<string>($"SELECT canal AS \"Value\" FROM interacoes WHERE id = {porEmail.Id}")
            .SingleAsync();
        gravado.Should().Be("EMAIL");
    }

    [Fact]
    public async Task AcessoPortal_ValidaTokenDepoisDeRelido()
    {
        var cliente = Dados.Cliente(_tenant);
        var solicitacao = Dados.Solicitacao(_tenant, cliente.Id);
        var (acesso, token) = Dados.Acesso(_tenant, cliente.Id, solicitacao.Id);
        await Gravar(cliente, solicitacao, acesso);

        await using var db = banco.DaEquipe(_tenant);
        var hash = AcessoPortal.HashDoToken(token);
        var lido = await db.AcessosPortal.SingleAsync(a => a.TokenHash == hash);
        lido.Validar(token, Dados.Agora.AddDays(1)).Should().BeTrue();
    }

    [Fact] // F15-51 (design D3 e D9)
    public async Task ConversaPorEmail_TokenEFioGravadosERelidos()
    {
        var cliente = Dados.Cliente(_tenant);
        var interna = Dados.Solicitacao(_tenant, cliente.Id);
        var porEmail = Dados.Solicitacao(_tenant, cliente.Id);
        var conversa = Dados.Conversa(_tenant, cliente.Id, interna.Id);
        var aberta = ConversaPorEmail.DaAberturaPorEmail(
            Guid.NewGuid(), _tenant, cliente.Id, porEmail.Id, "<abc@mail.gmail.com>", "Forno parou", Dados.Agora);
        await Gravar(cliente, interna, porEmail, conversa, aberta);

        await using var db = banco.DaEquipe(_tenant);
        var lida = await db.ConversasPorEmail.SingleAsync(c => c.SolicitacaoId == interna.Id);
        lida.Token.Should().Be(conversa.Token);
        lida.ClienteId.Should().Be(cliente.Id);
        lida.MessageIdOrigem.Should().BeNull();
        lida.CriadaEm.Should().Be(Dados.Agora);
        var doEmail = await db.ConversasPorEmail.SingleAsync(c => c.Token == aberta.Token);
        doEmail.MessageIdOrigem.Should().Be("<abc@mail.gmail.com>");
        doEmail.AssuntoOrigem.Should().Be("Forno parou");
    }

    [Fact] // design D9: uma conversa por solicitação
    public async Task ConversaPorEmail_SegundaDaMesmaSolicitacao_IndiceUnicoRecusa()
    {
        var cliente = Dados.Cliente(_tenant);
        var solicitacao = Dados.Solicitacao(_tenant, cliente.Id);
        await Gravar(cliente, solicitacao, Dados.Conversa(_tenant, cliente.Id, solicitacao.Id));

        var acao = () => Gravar(Dados.Conversa(_tenant, cliente.Id, solicitacao.Id));

        await acao.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Calendario_ExpedienteEFeriados()
    {
        var calendario = CalendarioUtil.Padrao(_tenant);
        calendario.DefinirFeriados([new DateOnly(2026, 11, 2), new DateOnly(2026, 10, 12)]);
        await Gravar(calendario);

        await using (var db = banco.DaEquipe(_tenant))
        {
            var lido = await db.Calendarios.SingleAsync();
            lido.Expediente.Should().HaveCount(5);
            lido.Feriados.Should().Equal(new DateOnly(2026, 10, 12), new DateOnly(2026, 11, 2));
            lido.JanelaDe(new DateOnly(2026, 10, 12)).Should().BeNull();

            lido.DefinirExpediente([new JanelaDiaria(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))]);
            await db.SaveChangesAsync();
        }

        await using var outro = banco.DaEquipe(_tenant);
        var relido = await outro.Calendarios.SingleAsync();
        relido.Expediente.Should().ContainSingle();
        relido.JanelaDe(new DateOnly(2026, 10, 5))!.Inicio.Should().Be(new TimeOnly(9, 0));
    }

    [Fact]
    public async Task Concorrencia_SegundaGravacaoComVersaoVelha_Falha()
    {
        var cliente = Dados.Cliente(_tenant);
        await Gravar(cliente);

        await using var primeira = banco.DaEquipe(_tenant);
        await using var segunda = banco.DaEquipe(_tenant);
        var a = await primeira.Clientes.SingleAsync(c => c.Id == cliente.Id);
        var b = await segunda.Clientes.SingleAsync(c => c.Id == cliente.Id);

        a.Editar("Nome A", null, null);
        await primeira.SaveChangesAsync();
        b.Editar("Nome B", null, null);

        var acao = () => segunda.SaveChangesAsync();

        await acao.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    // A convenção snake_case vale também para tipos lidos por SqlQuery: Tipo → tipo, FranquiaMinutos → franquia_minutos.
    private sealed class LinhaContrato
    {
        public string Tipo { get; set; } = "";

        public int? FranquiaMinutos { get; set; }
    }
}
