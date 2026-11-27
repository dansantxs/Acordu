using System.Security.Cryptography;
using System.Text;
using Acordu.Application.EntradaEmail;
using Acordu.Infrastructure.Email.Entrada;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Acordu.Api.EntradaEmail;

/// <summary>
/// Rota de entrada por e-mail (RF-SOL-11; design do F15, D1, D5 e D12; ADR 0016, itens 1 e 2; contrato §18): o Worker
/// <c>acordu-email-entrada</c> repassa, pela borda, o MIME bruto de cada e-mail que o Cloudflare Email Routing entrega.
/// Como os jobs: fora do OpenAPI, token em <c>X-Email-Token</c> comparado em tempo constante e <c>404</c> sem ele (ou sem
/// <c>EmailEntrada__Token</c> configurado). Respostas: <c>202</c> sem corpo para tudo o que foi tratado ou ignorado de
/// propósito, sem dizer qual; <c>413</c> acima do tamanho máximo (F15-89). Erro inesperado sai <c>5xx</c>, e o Worker
/// devolve falha temporária para o servidor de origem repetir (a idempotência absorve a repetição).
/// </summary>
internal static class EntradaEmailEndpoints
{
    public const string Rota = "/internal/email/entrada";

    public const string CabecalhoToken = "X-Email-Token";

    /// <summary>Destinatário do envelope (o endereço de entrada que recebeu).</summary>
    public const string CabecalhoDestinatario = "X-Email-Destinatario";

    /// <summary>Remetente do envelope (vazio numa devolução; design D12).</summary>
    public const string CabecalhoRemetente = "X-Email-Remetente";

    /// <summary>Veredito do filtro do provedor: <c>ok</c>, <c>spam</c> ou <c>virus</c> (design D11, F15-87).</summary>
    public const string CabecalhoFiltro = "X-Email-Filtro";

    /// <summary>
    /// Veredito de autenticidade do Worker: <c>pass</c>, <c>fail</c> ou <c>none</c> (design D11); só <c>pass</c> deixa o
    /// remetente ser conhecido (F15-26, F15-27).
    /// </summary>
    public const string CabecalhoAutenticidade = "X-Email-Autenticidade";

    public static void MapEntradaEmail(this IEndpointRouteBuilder app, IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        var esperado = configuracao["EmailEntrada:Token"];
        app.MapPost(Rota, async (HttpContext http, [FromServices] RecebedorDeEmail recebedor, [FromServices] OpcoesEmailEntrada opcoes) =>
            {
                if (!TokenValido(http, esperado))
                    return Results.NotFound();

                // Tamanho declarado acima do limite: recusa antes de ler (F15-89). Sem Content-Length (corpo em partes),
                // a leitura conta os bytes e recusa ao passar.
                var limite = opcoes.TamanhoMaximoBytes;
                if (http.Request.ContentLength > limite)
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } maximo)
                    maximo.MaxRequestBodySize = limite;

                DesfechoEntrada desfecho;
                try
                {
                    var cabecalhos = http.Request.Headers;
                    var envelope = new EnvelopeDeEntrada(
                        cabecalhos[CabecalhoDestinatario].ToString(),
                        cabecalhos.TryGetValue(CabecalhoRemetente, out var remetente) ? remetente.ToString() : null,
                        cabecalhos.TryGetValue(CabecalhoFiltro, out var filtro) ? filtro.ToString() : null,
                        cabecalhos.TryGetValue(CabecalhoAutenticidade, out var autenticidade) ? autenticidade.ToString() : null);
                    desfecho = await recebedor.ReceberAsync(envelope, http.Request.Body, http.RequestAborted);
                }
                catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
                {
                    desfecho = DesfechoEntrada.GrandeDemais;
                }

                return desfecho == DesfechoEntrada.GrandeDemais
                    ? Results.StatusCode(StatusCodes.Status413PayloadTooLarge)
                    : Results.StatusCode(StatusCodes.Status202Accepted);
            })
            .ExcludeFromDescription();
    }

    private static bool TokenValido(HttpContext http, string? esperado)
    {
        if (string.IsNullOrEmpty(esperado))
            return false;

        var recebido = http.Request.Headers[CabecalhoToken].ToString();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(recebido), Encoding.UTF8.GetBytes(esperado));
    }
}
