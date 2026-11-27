using Acordu.Application.EntradaEmail;
using MimeKit;
using MimeKit.Text;

namespace Acordu.Infrastructure.Email.Entrada;

/// <summary>
/// Leitura do e-mail recebido (design do F15, D1 e D14; ADR 0016, item 1): o MIME bruto do corpo da rota, interpretado
/// pelo MimeKit (biblioteca de protocolo, licença MIT, sem conta nem serviço; ADR 0010 §10, como o <c>AWSSDK.S3</c>). Só o
/// que a entrada usa sai daqui (<see cref="MensagemDeEntrada"/>); a mensagem bruta não é guardada.
/// </summary>
public static class LeitorMime
{
    /// <summary>
    /// O e-mail lido, ou nulo quando o corpo não é um e-mail (o servidor de origem não deve repetir: a rota responde
    /// <c>202</c>). Acima de <paramref name="tamanhoMaximoBytes"/> lança <see cref="CorpoGrandeDemaisException"/>.
    /// </summary>
    public static async Task<MensagemDeEntrada?> LerAsync(Stream corpo, long tamanhoMaximoBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(corpo);
        await using var limitado = new CorpoLimitado(corpo, tamanhoMaximoBytes);
        using var mensagem = await CarregarAsync(limitado, cancellationToken);
        // O que o parser não consumiu (epílogo, lixo depois da mensagem) também conta no tamanho e no hash.
        await limitado.CopyToAsync(Stream.Null, cancellationToken);
        if (mensagem is null)
            return null;

        var caixas = mensagem.From.Mailboxes.ToList();
        return new MensagemDeEntrada(
            ChaveDeEntrada.NormalizarMessageId(mensagem.MessageId), [.. caixas.Select(m => m.Address)], limitado.Hash(), Sinais(mensagem),
            Assunto: mensagem.Subject, Texto: Texto(mensagem), Anexos: Anexos(mensagem),
            NomeDoRemetente: caixas.Count == 1 ? caixas[0].Name : null);
    }

    /// <summary>
    /// As partes que não são o corpo em texto, na ordem do MIME (design D14; F15-14, F15-15), já decodificadas: anexos,
    /// imagens embutidas no corpo (<c>cid:</c>, pergunta 4), mensagens anexadas e as partes de um e-mail cifrado ou
    /// assinado. O corpo é a parte <c>text/plain</c> ou <c>text/html</c> que não é anexo (o <see cref="Texto"/> lê a
    /// primeira). Quem decide o que entra é a Application (<c>AnexosDaEntrada</c>), pelo conteúdo.
    /// </summary>
    private static List<AnexoDeEntrada> Anexos(MimeMessage mensagem)
    {
        var anexos = new List<AnexoDeEntrada>();
        if (mensagem.Body is { } corpo)
            Percorrer(corpo, null, anexos);
        return anexos;
    }

    /// <param name="tipoDoCifrado">Dentro de um <c>multipart/encrypted</c>, o tipo dele, para nenhuma parte entrar (D14).</param>
    private static void Percorrer(MimeEntity entidade, string? tipoDoCifrado, List<AnexoDeEntrada> anexos)
    {
        switch (entidade)
        {
            case Multipart multipart:
            {
                var cifrado = tipoDoCifrado
                    ?? (multipart.ContentType.IsMimeType("multipart", "encrypted") ? multipart.ContentType.MimeType : null);
                foreach (var filha in multipart)
                    Percorrer(filha, cifrado, anexos);
                break;
            }

            case MessagePart anexada:
                // Mensagem anexada nunca entra (D14): o conteúdo não importa, só o nome e o tipo.
                anexos.Add(new AnexoDeEntrada(
                    anexada.ContentDisposition?.FileName ?? anexada.ContentType.Name,
                    tipoDoCifrado ?? anexada.ContentType.MimeType,
                    []));
                break;
            case MimePart parte when tipoDoCifrado is not null || !EhCorpo(parte):
                anexos.Add(new AnexoDeEntrada(parte.FileName, tipoDoCifrado ?? parte.ContentType.MimeType, Conteudo(parte)));
                break;
        }
    }

    private static bool EhCorpo(MimePart parte) => parte is TextPart texto && !parte.IsAttachment && (texto.IsPlain || texto.IsHtml);

    private static byte[] Conteudo(MimePart parte)
    {
        if (parte.Content is null)
            return [];
        using var conteudo = new MemoryStream();
        parte.Content.DecodeTo(conteudo);
        return conteudo.ToArray();
    }

    /// <summary>
    /// O corpo só como texto (design D14; F15-13): a parte <c>text/plain</c> tem preferência; sem ela, o HTML vira texto pelo
    /// conversor do MimeKit (scripts, estilos e comentários saem, blocos viram quebras de linha, entidades decodificadas).
    /// Nenhum HTML sai daqui. Sem corpo, nulo.
    /// </summary>
    private static string? Texto(MimeMessage mensagem)
    {
        if (mensagem.TextBody is { } texto)
            return texto;
        return mensagem.HtmlBody is { } html ? new HtmlToText().Convert(html) : null;
    }

    /// <summary>Os cabeçalhos da lista do e-mail automático (design D12), como vieram.</summary>
    private static SinaisDeAutomatico Sinais(MimeMessage mensagem)
    {
        var cabecalhos = mensagem.Headers;
        return new SinaisDeAutomatico(
            AutoSubmitted: cabecalhos["Auto-Submitted"]?.Trim(),
            Precedence: cabecalhos["Precedence"]?.Trim(),
            ListaDeDistribuicao: cabecalhos.Contains("List-Id") || cabecalhos.Contains("List-Unsubscribe"),
            RespostaAutomatica: cabecalhos.Contains("X-Autoreply") || cabecalhos.Contains("X-Autorespond"),
            TipoDoCorpo: mensagem.Body?.ContentType.MimeType);
    }

    private static async Task<MimeMessage?> CarregarAsync(Stream corpo, CancellationToken cancellationToken)
    {
        try
        {
            return await MimeMessage.LoadAsync(corpo, cancellationToken);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
