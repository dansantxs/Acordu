namespace Acordu.Domain.Enums;

/// <summary>
/// Por que um e-mail recebido foi à triagem em vez de abrir uma solicitação (RF-SOL-15; design do F15, D10; F15-70): o
/// remetente não é "conhecido" (um contato ativo, de cliente ativo, com o domínio confirmado).
/// </summary>
public enum MotivoTriagem
{
    /// <summary>Nenhum contato da empresa tem o e-mail do remetente, ou o <c>From</c> não tem um endereço só (F15-20).</summary>
    Desconhecido = 1,

    /// <summary>Só contatos inativos, ou de clientes inativos, têm o e-mail do remetente (F15-25).</summary>
    ContatoInativo = 2,

    /// <summary>Contatos ativos de mais de um cliente ativo têm o e-mail do remetente (F15-24, pergunta 5).</summary>
    MaisDeUmCliente = 3,

    /// <summary>O remetente declarado é de um contato, mas o domínio não confirmou que o e-mail saiu dele (F15-26, D11).</summary>
    RemetenteNaoConfirmado = 4,

    /// <summary>Resposta ao endereço de uma conversa de quem não é contato ativo do cliente dela (F15-39, no F15j).</summary>
    NaoContatoDaSolicitacao = 5,
}
