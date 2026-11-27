using Acordu.Application.Anexos;

namespace Acordu.Application.EntradaEmail;

/// <summary>
/// Uma parte do e-mail recebido que não é o corpo em texto (design do F15, D14): anexo, imagem embutida no corpo
/// (<c>cid:</c>, pergunta 4), mensagem anexada ou parte cifrada ou assinada, já decodificada pela Infrastructure, na ordem
/// do MIME. Ainda não validada: quem decide o que entra é <see cref="AnexosDaEntrada"/>. O nome nunca vai ao log.
/// </summary>
public sealed class AnexoDeEntrada
{
    private readonly byte[] _conteudo;

    /// <param name="nomeArquivo">O nome como veio (<c>filename</c> ou <c>name</c>); nulo sem ele (comum na imagem embutida).</param>
    /// <param name="tipoDeclarado">
    /// O tipo MIME declarado pela parte: não decide o tipo do arquivo (que é detectado pelo conteúdo), só recusa o que nunca
    /// entra (mensagem anexada, parte cifrada ou assinada). A parte dentro de um <c>multipart/encrypted</c> vem com o tipo dele.
    /// </param>
    /// <param name="conteudo">O conteúdo decodificado (base64 ou quoted-printable já desfeitos).</param>
    public AnexoDeEntrada(string? nomeArquivo, string? tipoDeclarado, byte[] conteudo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        NomeArquivo = nomeArquivo;
        TipoDeclarado = tipoDeclarado;
        _conteudo = conteudo;
    }

    public string? NomeArquivo { get; }

    public string? TipoDeclarado { get; }

    public long TamanhoBytes => _conteudo.LongLength;

    /// <summary>Os primeiros bytes, o bastante para detectar o tipo (<see cref="TiposDeArquivo.BytesParaDetectar"/>).</summary>
    public ReadOnlySpan<byte> Inicio => _conteudo.AsSpan(0, Math.Min(TiposDeArquivo.BytesParaDetectar, _conteudo.Length));

    /// <summary>Um stream novo, só leitura, a cada chamada (como o <see cref="ArquivoRecebido.Abrir"/>).</summary>
    public Stream Abrir() => new MemoryStream(_conteudo, writable: false);
}
