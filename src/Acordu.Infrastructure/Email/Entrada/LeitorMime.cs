using Acordu.Application.EntradaEmail;
using MimeKit;

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

        var remetentes = mensagem.From.Mailboxes.Select(m => m.Address).ToList();
        return new MensagemDeEntrada(
            ChaveDeEntrada.NormalizarMessageId(mensagem.MessageId), remetentes, limitado.Hash(), Sinais(mensagem));
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
