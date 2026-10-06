using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// ADR 0006: a outbox da requisição é entregue antes da resposta (vale também no plano B serverless); o que falhar
/// fica para o job <c>tick</c>.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class OutboxNaRequisicaoTests(ApiFixture api)
{
    private readonly Guid _tenant = Guid.NewGuid();

    private async Task<(HttpStatusCode Status, Guid SolicitacaoId)> AbrirSolicitacaoAsync()
    {
        using var cliente = api.NovoHttp();
        using var resposta = await cliente.PostAsync(new Uri($"/api/v1/teste/solicitacoes/{_tenant}", UriKind.Relative), null);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        return (resposta.StatusCode, corpo.TryGetProperty("id", out var id) ? id.GetGuid() : Guid.Empty);
    }

    private async Task<MensagemOutbox> MensagemAsync()
    {
        await using var db = api.DaEquipe(_tenant);
        return await db.Set<MensagemOutbox>().SingleAsync(m => m.TenantId == _tenant);
    }

    private async Task<HttpResponseMessage> TickAsync(string? token)
    {
        using var cliente = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/tick", UriKind.Relative));
        if (token is not null)
            requisicao.Headers.Add("X-Job-Token", token);
        return await cliente.SendAsync(requisicao);
    }

    [Fact]
    public async Task CasoDeUso_EntregaAOutboxAntesDaResposta()
    {
        var (status, solicitacaoId) = await AbrirSolicitacaoAsync();

        status.Should().Be(HttpStatusCode.OK);
        api.Entregas.Entregues.Should().ContainSingle(e => e.SolicitacaoId == solicitacaoId)
            .Which.TenantId.Should().Be(_tenant);
        (await MensagemAsync()).ProcessadaEm.Should().NotBeNull();
    }

    [Fact]
    public async Task ManipuladorFalha_RespostaNaoMudaEMensagemFicaParaOTick()
    {
        api.Entregas.FalharPara(_tenant);

        var (status, solicitacaoId) = await AbrirSolicitacaoAsync();

        status.Should().Be(HttpStatusCode.OK);
        var mensagem = await MensagemAsync();
        mensagem.ProcessadaEm.Should().BeNull();
        mensagem.Tentativas.Should().Be(1);

        // O tick, com o token, reentrega quando a próxima tentativa vence.
        api.Entregas.PararDeFalhar(_tenant);
        api.Relogio.Agora = mensagem.ProximaTentativaEm;
        using var tick = await TickAsync(ApiFixture.TokenJobs);

        tick.StatusCode.Should().Be(HttpStatusCode.OK);
        (await tick.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("outboxProcessadas").GetInt32().Should().BePositive();
        api.Entregas.Entregues.Should().ContainSingle(e => e.SolicitacaoId == solicitacaoId);
        (await MensagemAsync()).ProcessadaEm.Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("token-errado")]
    public async Task Tick_SemTokenValido_404(string? token)
    {
        using var resposta = await TickAsync(token);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
