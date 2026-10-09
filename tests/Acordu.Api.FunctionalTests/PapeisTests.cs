using System.Net;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §3 e segurança §3.3: papéis padrão imutáveis e papéis sob medida.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class PapeisTests(ApiFixture api)
{
    [Fact]
    public async Task Listar_PadroesNaOrdem_ComAsPessoas_ECatalogo()
    {
        var equipe = await Equipe.CriarAsync(api);
        await equipe.ComPapelAsync(Papel.NomeLeitor);

        var papeis = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/papeis");
        var catalogo = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/papeis/permissoes");

        papeis.EnumerateArray().Select(p => p.GetProperty("nome").GetString())
            .Should().Equal(Papel.NomeDono, Papel.NomeMembro, Papel.NomeLeitor, Papel.NomeCliente);
        papeis[0].GetProperty("pessoas").GetInt32().Should().Be(1);
        papeis[2].GetProperty("pessoas").GetInt32().Should().Be(1);
        papeis[3].GetProperty("tipo").GetString().Should().Be("CLIENTE");
        papeis.EnumerateArray().Should().OnlyContain(p => p.GetProperty("padrao").GetBoolean());
        catalogo.EnumerateArray().Select(p => p.GetString()).Should().Contain(Permissoes.AuditoriaVer)
            .And.NotContain(Permissoes.PortalSolicitacaoVer);
    }

    [Fact]
    public async Task SobMedida_CriarEditarAtribuirEExcluir()
    {
        var equipe = await Equipe.CriarAsync(api);
        var financeiro = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/papeis", new
        {
            nome = "Financeiro", permissoes = new[] { Permissoes.ContratoVer, Permissoes.ContratoAjustarSaldo, Permissoes.ContratoVer },
        });
        var id = Cenarios.Id(financeiro);

        financeiro.GetProperty("tipo").GetString().Should().Be("INTERNO");
        financeiro.GetProperty("padrao").GetBoolean().Should().BeFalse();
        financeiro.GetProperty("permissoes").GetArrayLength().Should().Be(2);

        var editado = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/papeis/{id}", new
        {
            nome = "Financeiro e contratos", permissoes = new[] { Permissoes.ContratoVer, Permissoes.ContratoGerenciar },
        });
        editado.GetProperty("nome").GetString().Should().Be("Financeiro e contratos");

        // Atribuído a alguém, vale no token seguinte e não pode ser excluído.
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var usuario = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: membro))
            .GetProperty("usuarioId").GetGuid();
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/usuarios/{usuario}/papel", new { papelId = id });
        await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Delete, $"/api/v1/papeis/{id}");

        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/usuarios/{usuario}/papel",
            new { papelId = await PapelAsync(equipe, Papel.NomeMembro) });
        var email = $"pessoa-{Guid.NewGuid():N}@estudio.com";
        await equipe.EsperarAsync(HttpStatusCode.Accepted, HttpMethod.Post, "/api/v1/convites", new { email, papelId = id });
        var token = api.Emails.TokenDoConviteEquipe(email);

        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/papeis/{id}");

        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/convites")).GetArrayLength()
            .Should().Be(0, "excluir o papel revoga os convites pendentes com ele");
        using var http = api.NovoHttp();
        using var consulta = await http.PostAsync(
            new Uri("/api/v1/auth/convite/consulta", UriKind.Relative), System.Net.Http.Json.JsonContent.Create(new { token }));
        consulta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Recusas()
    {
        var equipe = await Equipe.CriarAsync(api);
        var dono = await PapelAsync(equipe, Papel.NomeDono);

        var portal = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/papeis",
            new { nome = "Portal", permissoes = new[] { Permissoes.PortalSolicitacaoVer } });
        portal.GetProperty("errors").TryGetProperty("permissoes", out _).Should().BeTrue();
        var vazio = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/papeis",
            new { nome = "Vazio", permissoes = Array.Empty<string>() });
        vazio.GetProperty("errors").TryGetProperty("permissoes", out _).Should().BeTrue();
        var repetido = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/papeis",
            new { nome = "membro", permissoes = new[] { Permissoes.ClienteVer } });
        repetido.GetProperty("errors").TryGetProperty("nome", out _).Should().BeTrue();
        await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Patch, $"/api/v1/papeis/{dono}",
            new { nome = "Chefe", permissoes = new[] { Permissoes.ClienteVer } });
        await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Delete, $"/api/v1/papeis/{dono}");
        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Delete, $"/api/v1/papeis/{Guid.NewGuid()}");

        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Get, "/api/v1/papeis", token: membro);
        var outra = await Equipe.CriarAsync(api);
        await outra.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Delete, $"/api/v1/papeis/{dono}");
    }

    private static async Task<Guid> PapelAsync(Equipe equipe, string nome)
    {
        var papeis = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/papeis");
        return papeis.EnumerateArray().Single(p => p.GetProperty("nome").GetString() == nome).GetProperty("id").GetGuid();
    }
}
