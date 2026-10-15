using System.Net;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>RF-COL-02: menções em interação interna e comentário de tarefa, com sino e e-mail (categoria MENCAO).</summary>
[Collection(ColecaoApi.Nome)]
public sealed class MencoesTests(ApiFixture api)
{
    private static async Task<(Guid Id, string Email)> EuAsync(Equipe equipe, string token)
    {
        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: token);
        return (eu.GetProperty("usuarioId").GetGuid(), eu.GetProperty("email").GetString()!);
    }

    [Fact]
    public async Task ComentarioDeTarefa_AvisaOMencionadoComONomeNoTrecho()
    {
        var equipe = await Equipe.CriarAsync(api);
        var membroToken = await equipe.ComPapelAsync(Papel.NomeMembro);
        var (membro, email) = await EuAsync(equipe, membroToken);
        var tarefa = await TarefaAsync(equipe, titulo: "Arte do cardápio");

        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/tarefas/{tarefa}/comentarios", new
        {
            texto = $"@{{{membro}}} consegue revisar hoje?",
        });

        var sino = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes", token: membroToken);
        var item = sino.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("categoria").GetString() == "MENCAO");
        item.GetProperty("titulo").GetString().Should().Be("Pessoa da equipe mencionou você na tarefa Arte do cardápio");
        item.GetProperty("link").GetString().Should().EndWith($"/tarefas/{tarefa}");
        var mensagem = api.Emails.Para(email).Single(m => m.Categoria == "aviso-mencao");
        mensagem.Texto.Should().Contain("\"@Pessoa da equipe consegue revisar hoje?\"");
    }

    [Fact]
    public async Task InteracaoInterna_Avisa_ExternaOuForaDaEquipe400()
    {
        var equipe = await Equipe.CriarAsync(api);
        var membroToken = await equipe.ComPapelAsync(Papel.NomeMembro);
        var (membro, _) = await EuAsync(equipe, membroToken);
        var cliente = await ClienteAsync(equipe);
        var solicitacao = await SolicitacaoAsync(equipe, cliente);
        var rota = $"/api/v1/solicitacoes/{solicitacao}/interacoes";

        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, rota, new { texto = $"@{{{membro}}} assume?", visibilidade = "INTERNA" });
        var externa = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Post, rota, new { texto = $"@{{{membro}}} oi", visibilidade = "EXTERNA" });
        var estranho = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Post, rota, new { texto = $"@{{{Guid.NewGuid()}}} oi", visibilidade = "INTERNA" });

        var sino = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes", token: membroToken);
        sino.GetProperty("itens").EnumerateArray().Count(i => i.GetProperty("categoria").GetString() == "MENCAO").Should().Be(1);
        externa.GetProperty("errors").TryGetProperty("texto", out _).Should().BeTrue();
        estranho.GetProperty("errors").TryGetProperty("texto", out _).Should().BeTrue();
    }
}
