using Acordu.Application.Abstractions;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Acordu.Infrastructure.Armazenamento;

/// <summary>
/// <see cref="IArmazenamentoArquivos"/> sobre o AWSSDK.S3 (só como cliente do protocolo), apontado para o Cloudflare R2
/// em produção (ADR 0010) ou para o S3Mock nos testes e no desenvolvimento. Ajustes de compatibilidade: path style,
/// checksums só quando exigidos e upload sem <c>aws-chunked</c> (provedores compatíveis com S3 nem sempre aceitam os
/// padrões novos do SDK v4).
/// </summary>
public sealed class ArmazenamentoS3 : IArmazenamentoArquivos, IDisposable
{
    private readonly AmazonS3Client _s3;
    private readonly string _bucket;
    private readonly Protocol _protocolo;

    public ArmazenamentoS3(OpcoesArmazenamento opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        if (!opcoes.Configurado)
            throw new InvalidOperationException("Endpoint do armazenamento não configurado (Armazenamento__Endpoint).");
        var endpoint = new Uri(opcoes.Endpoint!);
        var config = new AmazonS3Config
        {
            ServiceURL = endpoint.ToString(),
            ForcePathStyle = true,
            AuthenticationRegion = opcoes.Regiao,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        _s3 = new AmazonS3Client(new BasicAWSCredentials(opcoes.ChaveAcesso, opcoes.ChaveSecreta), config);
        _bucket = opcoes.Bucket;
        _protocolo = endpoint.Scheme == Uri.UriSchemeHttp ? Protocol.HTTP : Protocol.HTTPS;
    }

    public async Task EnviarAsync(
        string chave, Stream conteudo, long tamanhoBytes, string tipoConteudo, CancellationToken cancellationToken = default)
    {
        var pedido = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = chave,
            InputStream = conteudo,
            ContentType = tipoConteudo,
            AutoCloseStream = false,
            UseChunkEncoding = false,
        };
        pedido.Headers.ContentLength = tamanhoBytes;
        await _s3.PutObjectAsync(pedido, cancellationToken);
    }

    public async Task RemoverAsync(string chave, CancellationToken cancellationToken = default) =>
        await _s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucket, Key = chave }, cancellationToken);

    public Uri UrlDeDownload(string chave, string nomeArquivo, string tipoConteudo, TimeSpan validade)
    {
        var pedido = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = chave,
            Verb = HttpVerb.GET,
            Protocol = _protocolo,
            Expires = DateTime.UtcNow.Add(validade),
            // Sempre attachment (segurança §7): HTML/SVG nunca rodam no navegador pelo domínio do bucket.
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = ContentDisposition(nomeArquivo),
                ContentType = tipoConteudo,
            },
        };
        return new Uri(_s3.GetPreSignedURL(pedido));
    }

    public void Dispose() => _s3.Dispose();

    /// <summary>Nome ASCII de reserva (aspas e não ASCII viram <c>_</c>) e o original em <c>filename*</c> (RFC 6266).</summary>
    public static string ContentDisposition(string nomeArquivo)
    {
        var ascii = string.Concat(nomeArquivo.Select(c => c is < (char)32 or > (char)126 or '"' or '\\' ? '_' : c));
        return $"attachment; filename=\"{ascii}\"; filename*=UTF-8''{Uri.EscapeDataString(nomeArquivo)}";
    }
}

/// <summary>Sem storage configurado: comentários sem anexo funcionam; com anexo, erro claro.</summary>
public sealed class ArmazenamentoNaoConfigurado : IArmazenamentoArquivos
{
    private const string Mensagem = "Armazenamento de arquivos não configurado (Armazenamento__Endpoint).";

    public Task EnviarAsync(string chave, Stream conteudo, long tamanhoBytes, string tipoConteudo, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(Mensagem);

    public Task RemoverAsync(string chave, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Uri UrlDeDownload(string chave, string nomeArquivo, string tipoConteudo, TimeSpan validade) =>
        throw new InvalidOperationException(Mensagem);
}
