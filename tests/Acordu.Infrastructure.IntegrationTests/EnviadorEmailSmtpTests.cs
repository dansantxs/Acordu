using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Application.Abstractions;
using Acordu.Infrastructure.Email;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>O Mailpit do docker-compose.dev, numa versão fixa: SMTP na 1025 e a API de consulta na 8025.</summary>
public sealed class MailpitFixture : IAsyncLifetime
{
    private const int PortaSmtp = 1025;
    private const int PortaWeb = 8025;

    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.31.5")
        .WithPortBinding(PortaSmtp, true)
        .WithPortBinding(PortaWeb, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(PortaWeb).ForPath("/livez")))
        .Build();

    public string Host => _mailpit.Hostname;

    public int Smtp => _mailpit.GetMappedPublicPort(PortaSmtp);

    public Uri Api => new($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(PortaWeb)}/api/v1/");

    public Task InitializeAsync() => _mailpit.StartAsync();

    public Task DisposeAsync() => _mailpit.DisposeAsync().AsTask();
}

public sealed class EnviadorEmailSmtpTests(MailpitFixture mailpit) : IClassFixture<MailpitFixture>
{
    [Fact]
    public async Task Enviar_ChegaAoMailpitComRemetenteAssuntoETextoComLink()
    {
        var opcoes = new OpcoesEmail(null, "nao-responda@send.acordu.app.br", "Acordu", false, mailpit.Host, mailpit.Smtp);
        var enviador = new EnviadorEmailSmtp(opcoes, NullLogger<EnviadorEmailSmtp>.Instance);
        var para = $"ana-{Guid.NewGuid():N}@exemplo.com";

        await enviador.EnviarAsync(new MensagemEmail(
            para, "Redefina sua senha", "Abra o link: http://localhost:5173/conta/senha/abc", "redefinicao-senha", "Estúdio Traço"));

        using var http = new HttpClient { BaseAddress = mailpit.Api };
        var busca = await http.GetFromJsonAsync<JsonElement>($"search?query={Uri.EscapeDataString("to:" + para)}");
        var mensagem = busca.GetProperty("messages")[0];
        mensagem.GetProperty("Subject").GetString().Should().Be("Redefina sua senha");
        mensagem.GetProperty("From").GetProperty("Name").GetString().Should().Be("Estúdio Traço");
        mensagem.GetProperty("From").GetProperty("Address").GetString().Should().Be("nao-responda@send.acordu.app.br");
        var completa = await http.GetFromJsonAsync<JsonElement>($"message/{mensagem.GetProperty("ID").GetString()}");
        completa.GetProperty("Text").GetString().Should().Contain("http://localhost:5173/conta/senha/abc");
        completa.GetProperty("HTML").GetString().Should().Contain("<a href=\"http://localhost:5173/conta/senha/abc\">");
    }

    [Fact]
    public async Task Enviar_ComCabecalhos_ChegamAoMailpitComReplyTo() // F15-51, F15-55
    {
        var opcoes = new OpcoesEmail(null, "nao-responda@send.acordu.app.br", "Acordu", false, mailpit.Host, mailpit.Smtp);
        var enviador = new EnviadorEmailSmtp(opcoes, NullLogger<EnviadorEmailSmtp>.Instance);
        var para = $"carlos-{Guid.NewGuid():N}@exemplo.com";
        var cabecalhos = new Dictionary<string, string> { ["Auto-Submitted"] = "auto-generated" };

        await enviador.EnviarAsync(new MensagemEmail(
            para, "Nova mensagem na solicitação #3", "Olá!", "aviso-mensagem", "Estúdio Traço",
            "k7m2qdxz+abcdefghjkmnpqrs@entrada.acordu.app.br", cabecalhos));

        using var http = new HttpClient { BaseAddress = mailpit.Api };
        var busca = await http.GetFromJsonAsync<JsonElement>($"search?query={Uri.EscapeDataString("to:" + para)}");
        var id = busca.GetProperty("messages")[0].GetProperty("ID").GetString();
        var headers = await http.GetFromJsonAsync<JsonElement>($"message/{id}/headers");
        headers.GetProperty("Auto-Submitted")[0].GetString().Should().Be("auto-generated");
        headers.GetProperty("Reply-To")[0].GetString().Should().Contain("k7m2qdxz+abcdefghjkmnpqrs@entrada.acordu.app.br");
    }

    [Fact]
    public void SemServidor_Recusa()
    {
        var acao = () => new EnviadorEmailSmtp(new OpcoesEmail(null, "a@b.c", "Acordu", false), NullLogger<EnviadorEmailSmtp>.Instance);

        acao.Should().Throw<ArgumentException>();
    }
}
