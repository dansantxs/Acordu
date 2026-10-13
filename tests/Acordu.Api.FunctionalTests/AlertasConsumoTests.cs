using System.Net;
using Acordu.Api.FunctionalTests.Apoio;

namespace Acordu.Api.FunctionalTests;

/// <summary>RF-CTR-07: aviso ao cruzar 80% e 100% da franquia, uma vez por limiar, rearmado ao cair.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class AlertasConsumoTests(ApiFixture api)
{
    [Fact]
    public async Task MensalFixo_AvisaAoCruzar80E100_UmaVezCada_ERearmaAoCair()
    {
        var equipe = await Equipe.CriarAsync(api);
        var email = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("email").GetString()!;
        var cliente = await Cenarios.ClienteAsync(equipe);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", new
        {
            clienteId = cliente, tipo = "MENSAL_FIXO", inicio = "2026-09-01", franquiaMinutos = 600, sla = Cenarios.Sla(),
        });
        var tarefa = await Cenarios.TarefaAsync(equipe, await Cenarios.SolicitacaoAsync(equipe, cliente));
        var hoje = DateOnly.FromDateTime(api.Relogio.Agora.UtcDateTime).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        var primeiro = await Apontar(equipe, tarefa, 500, hoje);
        Avisos(email).Should().ContainSingle().Which.Assunto.Should().Contain("80%");

        await Apontar(equipe, tarefa, 50, hoje);
        Avisos(email).Should().HaveCount(1, "continua acima de 80%: não repete");

        await Apontar(equipe, tarefa, 70, hoje);
        Avisos(email).Should().HaveCount(2);
        Avisos(email)[1].Assunto.Should().Contain("100%");
        Avisos(email)[1].Texto.Should().Contain("10h20 de 10h00");

        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/apontamentos/{primeiro}");
        Avisos(email).Should().HaveCount(2, "cair não avisa, só rearma");

        await Apontar(equipe, tarefa, 480, hoje);
        Avisos(email).Should().HaveCount(4, "os dois limiares foram rearmados e cruzados de novo");
    }

    [Fact]
    public async Task SemPermissaoDeContrato_NaoRecebe()
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Domain.Entities.Papel.NomeMembro);
        var emailMembro = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: membro))
            .GetProperty("email").GetString()!;
        var cliente = await Cenarios.ClienteAsync(equipe);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", new
        {
            clienteId = cliente, tipo = "MENSAL_FIXO", inicio = "2026-09-01", franquiaMinutos = 60, sla = Cenarios.Sla(),
        });
        var tarefa = await Cenarios.TarefaAsync(equipe, await Cenarios.SolicitacaoAsync(equipe, cliente));
        var hoje = DateOnly.FromDateTime(api.Relogio.Agora.UtcDateTime).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        await Apontar(equipe, tarefa, 90, hoje);

        Avisos(emailMembro).Should().BeEmpty("Membro não tem contrato.gerenciar");
    }

    private List<Application.Abstractions.MensagemEmail> Avisos(string email) =>
        [.. api.Emails.Para(email).Where(m => m.Categoria == "aviso-consumo")];

    private static async Task<Guid> Apontar(Equipe equipe, Guid tarefa, int minutos, string data) =>
        Cenarios.Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/apontamentos", new
        {
            tarefaId = tarefa, data, duracaoMinutos = minutos, descricao = "Trabalho", faturavel = true,
        }));
}
