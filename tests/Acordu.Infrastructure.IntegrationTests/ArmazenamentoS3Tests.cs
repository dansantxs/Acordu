using System.Net;
using System.Text;
using Acordu.Infrastructure.Armazenamento;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>
/// Emulador S3 (Adobe S3Mock) por classe: o mesmo protocolo do OCI Object Storage, com os ajustes de compatibilidade do
/// cliente. (As imagens públicas do MinIO deixaram de ser distribuídas.)
/// </summary>
public sealed class EmuladorS3Fixture : IAsyncLifetime
{
    public const string Bucket = "acordu-anexos-teste";
    private const int Porta = 9090;

    private readonly IContainer _s3 = new ContainerBuilder("adobe/s3mock:5.2.2")
        .WithPortBinding(Porta, true)
        .WithEnvironment("COM_ADOBE_TESTING_S3MOCK_STORE_INITIAL_BUCKETS", Bucket)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(Porta).ForPath("/")))
        .Build();

    public OpcoesArmazenamento Opcoes { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _s3.StartAsync();
        Opcoes = new OpcoesArmazenamento(
            $"http://{_s3.Hostname}:{_s3.GetMappedPublicPort(Porta)}", "us-east-1", Bucket, "chave-teste", "segredo-teste");
    }

    public Task DisposeAsync() => _s3.DisposeAsync().AsTask();
}

public sealed class ArmazenamentoS3Tests(EmuladorS3Fixture s3) : IClassFixture<EmuladorS3Fixture>
{
    private static readonly byte[] Conteudo = Encoding.UTF8.GetBytes("%PDF-1.7 relatório de entrega");

    [Fact]
    public async Task Enviar_EBaixarPelaUrlAssinada_ComoAttachment()
    {
        using var armazenamento = new ArmazenamentoS3(s3.Opcoes);
        var chave = $"{Guid.NewGuid()}/{Guid.NewGuid()}/{Guid.NewGuid()}";
        await using (var stream = new MemoryStream(Conteudo))
            await armazenamento.EnviarAsync(chave, stream, Conteudo.Length, "application/pdf");

        var url = armazenamento.UrlDeDownload(chave, "relatório final.pdf", "application/pdf", TimeSpan.FromMinutes(5));
        using var http = new HttpClient();
        using var resposta = await http.GetAsync(url);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.Content.ReadAsByteArrayAsync()).Should().Equal(Conteudo);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var disposicao = resposta.Content.Headers.ContentDisposition!;
        disposicao.DispositionType.Should().Be("attachment");
        disposicao.FileNameStar.Should().Be("relatório final.pdf");
    }

    [Fact]
    public async Task Remover_UrlNovaDa404_ERemoverDeNovoNaoFalha()
    {
        using var armazenamento = new ArmazenamentoS3(s3.Opcoes);
        var chave = $"{Guid.NewGuid()}/x/{Guid.NewGuid()}";
        await using (var stream = new MemoryStream(Conteudo))
            await armazenamento.EnviarAsync(chave, stream, Conteudo.Length, "application/pdf");

        await armazenamento.RemoverAsync(chave);
        await armazenamento.RemoverAsync(chave);

        using var http = new HttpClient();
        using var resposta = await http.GetAsync(armazenamento.UrlDeDownload(chave, "a.pdf", "application/pdf", TimeSpan.FromMinutes(5)));
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void ContentDisposition_NomeAsciiDeReservaEOriginalCodificado()
    {
        ArmazenamentoS3.ContentDisposition("orçamento \"v2\".pdf")
            .Should().Be("attachment; filename=\"or_amento _v2_.pdf\"; filename*=UTF-8''or%C3%A7amento%20%22v2%22.pdf");
    }
}
