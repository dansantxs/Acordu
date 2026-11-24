using Acordu.Application.Abstractions;
using Acordu.Application.Autenticacao;
using Acordu.Application.Common;

namespace Acordu.Api.Seguranca;

/// <summary>
/// O Turnstile das rotas públicas que gravam (RNF-SEG-10): o cadastro (F9c) e a lista de espera (F14o). Vem antes de
/// qualquer validação ou gravação; recusado ou indisponível → <c>400</c> em <c>errors.tokenTurnstile</c>, igual nas duas.
/// </summary>
internal static class DesafioHumano
{
    public const string CampoToken = "tokenTurnstile";

    public static async Task ExigirAsync(
        IVerificacaoHumana humana, IOrigemRequisicao origem, string? token, string acao, CancellationToken cancellationToken)
    {
        var verificacao = await humana.VerificarAsync(token, origem.Ip, acao, cancellationToken);
        if (verificacao != ResultadoVerificacaoHumana.Aprovada)
        {
            throw ErroDeCampo.Criar(
                CampoToken,
                verificacao == ResultadoVerificacaoHumana.Indisponivel
                    ? "Não deu para conferir agora. Tente de novo em instantes."
                    : "Confirme que você não é um robô e tente de novo.");
        }
    }
}
