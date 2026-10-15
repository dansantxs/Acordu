using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>RF-COL-03: seguimento automático e manual, avisos SEGUIDAS e e-mail desligado por padrão.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class SeguidoresTests(ApiFixture api)
{
    private static async Task<(Guid Id, string Email)> EuAsync(Equipe equipe, string? token = null)
    {
        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: token);
        return (eu.GetProperty("usuarioId").GetGuid(), eu.GetProperty("email").GetString()!);
    }

    private static async Task<JsonElement[]> SeguidasAsync(Equipe equipe, string? token = null) =>
        [.. (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes", token: token))
            .GetProperty("itens").EnumerateArray().Where(i => i.GetProperty("categoria").GetString() == "SEGUIDAS")];

    [Fact]
    public async Task Tarefa_CriadorEResponsavelSeguem_ComentarioEStatusAvisamOsOutros()
    {
        var equipe = await Equipe.CriarAsync(api);
        var (dono, emailDono) = await EuAsync(equipe);
        var membroToken = await equipe.ComPapelAsync(Papel.NomeMembro);
        var (membro, _) = await EuAsync(equipe, membroToken);
        var tarefa = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new
        {
            titulo = "Arte do cardápio", responsavelId = membro,
        }));

        var seguidores = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/tarefas/{tarefa}/seguidores");
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/tarefas/{tarefa}/comentarios", new { texto = "Subi a versão 2." }, membroToken);
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/tarefas/{tarefa}/inicio");

        seguidores.EnumerateArray().Select(Id).Should().BeEquivalentTo([dono, membro]);
        var doDono = await SeguidasAsync(equipe);
        doDono.Should().HaveCount(1);
        doDono[0].GetProperty("titulo").GetString().Should().Be("Pessoa da equipe comentou na tarefa Arte do cardápio");
        var doMembro = await SeguidasAsync(equipe, membroToken);
        doMembro.Should().HaveCount(1);
        doMembro[0].GetProperty("titulo").GetString().Should().Be("A tarefa Arte do cardápio agora está em andamento");
        api.Emails.Para(emailDono).Should().NotContain(m => m.Categoria == "aviso-seguidas", "SEGUIDAS nasce com o e-mail desligado");
    }

    [Fact]
    public async Task Solicitacao_SeguirManualEmailLigadoEDeixarDeSeguir()
    {
        var equipe = await Equipe.CriarAsync(api);
        var leitorToken = await equipe.ComPapelAsync(Papel.NomeLeitor);
        var (leitor, emailLeitor) = await EuAsync(equipe, leitorToken);
        var cliente = await ClienteAsync(equipe);
        var solicitacao = await SolicitacaoAsync(equipe, cliente);
        var rota = $"/api/v1/solicitacoes/{solicitacao}";

        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"{rota}/seguidores/eu", token: leitorToken);
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, "/api/v1/notificacoes/preferencias", new
        {
            categorias = new[] { new { categoria = "SEGUIDAS", email = true } },
        }, leitorToken);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"{rota}/interacoes", new { texto = "Conferindo com o cliente.", visibilidade = "INTERNA" });
        var seguindo = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{rota}/seguidores", token: leitorToken);
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"{rota}/seguidores/eu", token: leitorToken);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"{rota}/interacoes", new { texto = "Mais uma nota.", visibilidade = "INTERNA" });
        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Put, $"/api/v1/solicitacoes/{Guid.NewGuid()}/seguidores/eu", token: leitorToken);

        seguindo.EnumerateArray().Select(Id).Should().Contain(leitor);
        var avisos = await SeguidasAsync(equipe, leitorToken);
        avisos.Should().HaveCount(1);
        avisos[0].GetProperty("titulo").GetString().Should().StartWith("Pessoa da equipe deixou uma nota interna em #");
        api.Emails.Para(emailLeitor).Count(m => m.Categoria == "aviso-seguidas").Should().Be(1);
    }

    [Fact]
    public async Task Mencionado_RecebeMencaoESegue_SemAvisoDuplicado()
    {
        var equipe = await Equipe.CriarAsync(api);
        var membroToken = await equipe.ComPapelAsync(Papel.NomeMembro);
        var (membro, _) = await EuAsync(equipe, membroToken);
        var tarefa = await TarefaAsync(equipe);
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/v1/tarefas/{tarefa}/seguidores/eu", token: membroToken);

        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/tarefas/{tarefa}/comentarios", new { texto = $"@{{{membro}}} olha" });

        var sino = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes", token: membroToken))
            .GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("categoria").GetString()).ToList();
        sino.Should().Equal("MENCAO");
    }
}
