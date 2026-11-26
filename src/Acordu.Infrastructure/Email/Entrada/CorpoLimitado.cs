using System.Security.Cryptography;

namespace Acordu.Infrastructure.Email.Entrada;

/// <summary>
/// O corpo da rota de entrada lido em stream (design do F15, §1.2): conta os bytes e lança
/// <see cref="CorpoGrandeDemaisException"/> ao passar do limite, mesmo sem <c>Content-Length</c>, e calcula o SHA-256 do
/// que passou (a chave sem <c>Message-ID</c>, D6), sem guardar a mensagem bruta.
/// </summary>
internal sealed class CorpoLimitado : Stream
{
    private readonly Stream _interno;
    private readonly long _limite;
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private long _lidos;

    public CorpoLimitado(Stream interno, long limite)
    {
        ArgumentNullException.ThrowIfNull(interno);
        ArgumentOutOfRangeException.ThrowIfLessThan(limite, 0L);
        _interno = interno;
        _limite = limite;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _lidos;
        set => throw new NotSupportedException();
    }

    /// <summary>SHA-256 (hex minúsculo) de tudo o que foi lido. Chamado uma vez, no fim.</summary>
    public string Hash() => Convert.ToHexStringLower(_hash.GetHashAndReset());

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        var lidos = _interno.Read(buffer);
        Contar(buffer[..lidos]);
        return lidos;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var lidos = await _interno.ReadAsync(buffer, cancellationToken);
        Contar(buffer.Span[..lidos]);
        return lidos;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _hash.Dispose();
        base.Dispose(disposing);
    }

    private void Contar(ReadOnlySpan<byte> lidos)
    {
        _lidos += lidos.Length;
        if (_lidos > _limite)
            throw new CorpoGrandeDemaisException();
        _hash.AppendData(lidos);
    }
}
