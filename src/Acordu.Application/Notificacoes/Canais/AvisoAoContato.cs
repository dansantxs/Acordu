using Acordu.Domain.Enums;

namespace Acordu.Application.Notificacoes.Canais;

/// <summary>
/// Um aviso do RF-NOT-01 já montado, pronto para o canal (design do F15, D7). <see cref="Texto"/> já traz o link do
/// portal; <see cref="Categoria"/> identifica o tipo no log, sem dado do destinatário.
/// </summary>
/// <param name="ResponderPara">O "responder para" da empresa (F5a), ou nulo.</param>
/// <param name="EnderecoDaConversa">
/// O endereço da conversa da solicitação (D3), só com o recebimento por e-mail ligado; nulo com ele desligado (F15-52).
/// </param>
public sealed record AvisoAoContato(
    string Categoria,
    string NomeContato,
    string EmailContato,
    string Prestador,
    string Assunto,
    string Texto,
    Guid SolicitacaoId,
    int NumeroSolicitacao,
    OrigemSolicitacao Origem,
    string? ResponderPara,
    string? EnderecoDaConversa);
