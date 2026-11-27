namespace Acordu.Domain.Enums;

/// <summary>
/// Por que um anexo de e-mail recebido não entrou na solicitação (RNF-SEG-07; design do F15, D14; F15-15), na ordem em que
/// é conferido: tamanho, tipo, quantidade.
/// </summary>
public enum MotivoRecusaAnexo
{
    /// <summary>Acima do tamanho máximo por arquivo (20 MB).</summary>
    GrandeDemais = 1,

    /// <summary>
    /// Tipo fora da lista, detectado pelo conteúdo; também a mensagem anexada, a parte cifrada ou assinada e o arquivo vazio.
    /// </summary>
    TipoNaoPermitido = 2,

    /// <summary>Válido, mas além do 5º anexo do e-mail.</summary>
    AlemDoLimite = 3,
}
