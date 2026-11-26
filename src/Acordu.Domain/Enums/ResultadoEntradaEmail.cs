namespace Acordu.Domain.Enums;

/// <summary>
/// O que um e-mail aceito para uma empresa produziu (design do F15, D6; ADR 0016, item 8), guardado no
/// <c>EmailRecebido</c>. Os descartes antes da empresa (desconhecida, desligada, só leitura) não ganham registro.
/// </summary>
public enum ResultadoEntradaEmail
{
    /// <summary>Abriu uma solicitação (F15f).</summary>
    Solicitacao = 1,

    /// <summary>Entrou como mensagem numa conversa (F15i).</summary>
    Mensagem = 2,

    /// <summary>Foi à triagem (F15h).</summary>
    Triagem = 3,

    /// <summary>Ignorado de propósito: automático, laço, filtro ou, até o F15f, todo e-mail aceito (design §3).</summary>
    Ignorado = 4,

    /// <summary>Acima de um limite (F15n): não cria nada.</summary>
    Retido = 5,
}
