namespace Acordu.Application.Notificacoes.Canais;

/// <summary>
/// Canal por onde um aviso ao contato sai (RF-NOT-06; design do F15, D7; ADR 0016, item 7). No v1 só o e-mail tem
/// implementação; o WhatsApp entra no #184 como outra <see cref="ICanalDeAviso"/>.
/// </summary>
public enum CanalAviso
{
    Email = 1,
    Whatsapp = 2,
}
