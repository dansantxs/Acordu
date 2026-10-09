using System.Net;
using System.Text.Json;
using Acordu.Application.Abstractions;
using Acordu.Infrastructure.Email;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>Envio pelo Resend contra um handler HTTP falso: formato da requisição e erro fora de 2xx.</summary>
public sealed class EnviadorEmailResendTests
{
    private static readonly OpcoesEmail Opcoes = new("re_chave_de_teste", "nao-responda@send.acordu.app.br", "Acordu", false);

    [Fact]
    public async Task Enviar_PostComBearer_RemetenteDoPrestador_TextoEHtml()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK);
        using var http = new HttpClient(handler);
        var enviador = new EnviadorEmailResend(http, Opcoes);

        await enviador.EnviarAsync(new MensagemEmail(
            "marina@bomgrao.com.br",
            "Convite",
            "Olá, Marina <teste> & cia!\nAcesse https://acordu.app.br/portal/convite/abc",
            "convite-portal",
            "Estúdio \"Traço\" <ltda>"));

        handler.Requisicao!.Method.Should().Be(HttpMethod.Post);
        handler.Requisicao.RequestUri.Should().Be(EnviadorEmailResend.Endpoint);
        handler.Requisicao.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.Requisicao.Headers.Authorization.Parameter.Should().Be("re_chave_de_teste");
        var corpo = JsonDocument.Parse(handler.Corpo!).RootElement;
        corpo.GetProperty("from").GetString().Should().Be("Estúdio Traço ltda <nao-responda@send.acordu.app.br>");
        corpo.GetProperty("to")[0].GetString().Should().Be("marina@bomgrao.com.br");
        corpo.GetProperty("subject").GetString().Should().Be("Convite");
        corpo.GetProperty("text").GetString().Should().Contain("<teste>");
        var html = corpo.GetProperty("html").GetString()!;
        html.Should().Contain("&lt;teste&gt; &amp; cia!<br>")
            .And.Contain("<a href=\"https://acordu.app.br/portal/convite/abc\">");
    }

    [Fact]
    public async Task Enviar_SemNomeDoRemetente_UsaOPadrao()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK);
        using var http = new HttpClient(handler);

        await new EnviadorEmailResend(http, Opcoes).EnviarAsync(new MensagemEmail("a@b.com", "Oi", "Oi", "teste"));

        JsonDocument.Parse(handler.Corpo!).RootElement.GetProperty("from").GetString()
            .Should().Be("Acordu <nao-responda@send.acordu.app.br>");
    }

    [Fact]
    public async Task Enviar_RespostaDeErro_LancaSemExporOConteudo()
    {
        using var http = new HttpClient(new HandlerFalso(HttpStatusCode.UnprocessableEntity));
        var enviador = new EnviadorEmailResend(http, Opcoes);

        var acao = () => enviador.EnviarAsync(new MensagemEmail("marina@bomgrao.com.br", "Assunto", "Texto secreto", "convite-portal"));

        var erro = (await acao.Should().ThrowAsync<HttpRequestException>()).Which;
        erro.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        erro.Message.Should().Contain("convite-portal").And.NotContain("marina").And.NotContain("Texto secreto");
    }

    [Fact]
    public void Criar_SemChave_LancaExcecao()
    {
        using var http = new HttpClient();

        var acao = () => new EnviadorEmailResend(http, Opcoes with { ChaveResend = null });

        acao.Should().Throw<ArgumentException>();
    }

    private sealed class HandlerFalso(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Requisicao { get; private set; }

        public string? Corpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requisicao = request;
            Corpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent("{\"id\":\"e1\"}") };
        }
    }
}
