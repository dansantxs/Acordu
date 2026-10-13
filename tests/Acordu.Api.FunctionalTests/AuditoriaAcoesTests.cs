using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;

namespace Acordu.Api.FunctionalTests;

/// <summary>RNF-AUD-01 (9b): as ações sensíveis de equipe, portal, contratos, apontamentos e solicitações caem na trilha.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class AuditoriaAcoesTests(ApiFixture api)
{
    private static async Task<JsonElement[]> TrilhaAsync(Equipe equipe, string acao) =>
        [.. (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/auditoria?acao={acao}"))
            .GetProperty("itens").EnumerateArray()];

    private static async Task<JsonElement> UnicaAsync(Equipe equipe, string acao)
    {
        var trilha = await TrilhaAsync(equipe, acao);
        trilha.Should().HaveCount(1, "ação {0}", acao);
        return trilha[0];
    }

    private static async Task<Guid> EuAsync(Equipe equipe, string? token = null) =>
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: token)).GetProperty("usuarioId").GetGuid();

    private static async Task<Guid> PapelAsync(Equipe equipe, string nome) =>
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/papeis")).EnumerateArray()
            .Single(p => p.GetProperty("nome").GetString() == nome).GetProperty("id").GetGuid();

    [Fact]
    public async Task Equipe_ConviteTrocaDePapelERemocao()
    {
        var equipe = await Equipe.CriarAsync(api);
        var eu = await EuAsync(equipe);
        var leitorId = await PapelAsync(equipe, Papel.NomeLeitor);
        var membro = await EuAsync(equipe, await equipe.ComPapelAsync(Papel.NomeMembro));

        var email = $"convidada-{Guid.NewGuid():N}@estudio.com";
        var convite = Cenarios.Id(await equipe.EsperarAsync(
            HttpStatusCode.Accepted, HttpMethod.Post, "/api/v1/convites", new { email, papelId = leitorId }));
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/convites/{convite}");
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/usuarios/{membro}/papel", new { papelId = leitorId });
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/usuarios/{membro}");

        var convidado = (await TrilhaAsync(equipe, "equipe.convite")).Single(r => r.GetProperty("alvoId").GetGuid() == convite);
        convidado.GetProperty("usuario").GetProperty("id").GetGuid().Should().Be(eu);
        convidado.GetProperty("dados").GetProperty("papel").GetString().Should().Be(Papel.NomeLeitor);
        (await UnicaAsync(equipe, "equipe.convite.revogacao")).GetProperty("alvoId").GetGuid().Should().Be(convite);
        var troca = await UnicaAsync(equipe, "equipe.papel.troca");
        troca.GetProperty("alvoId").GetGuid().Should().Be(membro);
        troca.GetProperty("dados").GetProperty("de").GetString().Should().Be(Papel.NomeMembro);
        troca.GetProperty("dados").GetProperty("para").GetString().Should().Be(Papel.NomeLeitor);
        (await UnicaAsync(equipe, "equipe.remocao")).GetProperty("dados").GetProperty("papel").GetString().Should().Be(Papel.NomeLeitor);
    }

    [Fact]
    public async Task Contrato_CriacaoEdicaoAjusteEEncerramento()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contrato = await Cenarios.ContratoAsync(equipe, cliente);

        await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/contratos/{contrato}", new { franquiaMinutos = 2400, sla = Cenarios.Sla() });
        await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/contratos/{contrato}/ajustes", new { quantidadeMinutos = -60, motivo = "Cortesia" });
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/contratos/{contrato}/suspensao");
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/contratos/{contrato}/encerramento");

        (await UnicaAsync(equipe, "contrato.criacao")).GetProperty("dados").GetProperty("termos")
            .GetProperty("franquiaMinutos").GetInt32().Should().Be(1200);
        var edicao = (await UnicaAsync(equipe, "contrato.edicao")).GetProperty("dados");
        edicao.GetProperty("antes").GetProperty("franquiaMinutos").GetInt32().Should().Be(1200);
        edicao.GetProperty("depois").GetProperty("franquiaMinutos").GetInt32().Should().Be(2400);
        var ajuste = (await UnicaAsync(equipe, "contrato.ajuste_saldo")).GetProperty("dados");
        ajuste.GetProperty("quantidadeMinutos").GetInt32().Should().Be(-60);
        ajuste.GetProperty("motivo").GetString().Should().Be("Cortesia");
        (await UnicaAsync(equipe, "contrato.suspensao")).GetProperty("alvoId").GetGuid().Should().Be(contrato);
        (await UnicaAsync(equipe, "contrato.encerramento")).GetProperty("alvoId").GetGuid().Should().Be(contrato);
    }

    [Fact]
    public async Task Apontamento_EdicaoEExclusaoGuardamOValorAnterior()
    {
        var equipe = await Equipe.CriarAsync(api);
        var tarefa = await Cenarios.TarefaAsync(equipe);
        var apontamento = Cenarios.Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/apontamentos", new
        {
            tarefaId = tarefa, data = "2026-10-05", duracaoMinutos = 90, descricao = "Ajuste de layout", faturavel = true,
        }));

        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/apontamentos/{apontamento}", new
        {
            data = "2026-10-05", duracaoMinutos = 120, descricao = "Ajuste de layout", faturavel = true, visivelCliente = false,
        });
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/apontamentos/{apontamento}");

        var edicao = (await UnicaAsync(equipe, "apontamento.edicao")).GetProperty("dados");
        edicao.GetProperty("antes").GetProperty("duracaoMinutos").GetInt32().Should().Be(90);
        edicao.GetProperty("depois").GetProperty("duracaoMinutos").GetInt32().Should().Be(120);
        var exclusao = await UnicaAsync(equipe, "apontamento.exclusao");
        exclusao.GetProperty("alvoId").GetGuid().Should().Be(apontamento);
        exclusao.GetProperty("dados").GetProperty("antes").GetProperty("duracaoMinutos").GetInt32().Should().Be(120);
    }

    [Fact]
    public async Task Solicitacao_StatusEResponsavel()
    {
        var equipe = await Equipe.CriarAsync(api);
        var eu = await EuAsync(equipe);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, cliente);

        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/solicitacoes/{solicitacao}", new
        {
            titulo = "Ajustar banner da campanha", prioridade = "MEDIA", responsavelId = eu,
        });
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/inicio");

        var responsavel = (await UnicaAsync(equipe, "solicitacao.responsavel")).GetProperty("dados");
        responsavel.GetProperty("de").ValueKind.Should().Be(JsonValueKind.Null);
        responsavel.GetProperty("para").GetGuid().Should().Be(eu);
        var status = await UnicaAsync(equipe, "solicitacao.status");
        status.GetProperty("alvoId").GetGuid().Should().Be(solicitacao);
        status.GetProperty("usuario").GetProperty("id").GetGuid().Should().Be(eu);
        status.GetProperty("dados").GetProperty("de").GetString().Should().Be("ABERTA");
        status.GetProperty("dados").GetProperty("para").GetString().Should().Be("EM_ANDAMENTO");
    }

    [Fact]
    public async Task Portal_AcessoELinks()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, cliente);

        await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/clientes/{cliente}/contatos/{contato.Id}/acesso-portal",
            new { escopoPortal = "PROPRIAS" });
        var link = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal",
            new { contatoId = contato.Id, enviarEmail = false });
        var acessoId = link.GetProperty("acessoId").GetGuid();
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal/{acessoId}");
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/clientes/{cliente}/contatos/{contato.Id}/acesso-portal");

        (await UnicaAsync(equipe, "portal.acesso.concessao")).GetProperty("alvoId").GetGuid().Should().Be(contato.Id);
        (await UnicaAsync(equipe, "portal.acesso.escopo")).GetProperty("dados").GetProperty("escopo").GetString().Should().Be("PROPRIAS");
        var emissao = await UnicaAsync(equipe, "portal.link.emissao");
        emissao.GetProperty("alvoId").GetGuid().Should().Be(solicitacao);
        emissao.GetProperty("dados").GetProperty("acessoPortalId").GetGuid().Should().Be(acessoId);
        (await UnicaAsync(equipe, "portal.link.revogacao")).GetProperty("dados").GetProperty("acessoPortalId").GetGuid().Should().Be(acessoId);
        (await UnicaAsync(equipe, "portal.acesso.revogacao")).GetProperty("alvoId").GetGuid().Should().Be(contato.Id);
    }
}
