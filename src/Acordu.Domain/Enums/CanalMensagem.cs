namespace Acordu.Domain.Enums;

/// <summary>
/// Por onde uma mensagem da conversa entrou no Acordu (RF-SOL-14, ADR 0016 item 6), independente da origem da
/// solicitação: a aberta no portal pode receber resposta por e-mail. ACORDU é a equipe pelo app; o contato escreve
/// pelo PORTAL (conta ou link), por EMAIL ou, no #184, por WHATSAPP.
/// </summary>
public enum CanalMensagem
{
    Acordu = 1,
    Portal = 2,
    Email = 3,
    Whatsapp = 4,
}
