namespace Acordu.Application.Notificacoes.Canais;

/// <summary>
/// Porta de entrega dos avisos ao contato (RF-NOT-06; design do F15, D7; ADR 0016, item 7): o manipulador decide o quê e
/// para quem e monta o conteúdo; o canal só entrega, no formato dele. Chamada depois do commit (outbox): falha lança
/// exceção e a outbox tenta de novo.
/// </summary>
public interface ICanalDeAviso
{
    CanalAviso Canal { get; }

    /// <summary>Entrega o aviso. Falso quando este canal não consegue entregá-lo (o seletor usa então o e-mail).</summary>
    Task<bool> EnviarAsync(AvisoAoContato aviso, CancellationToken cancellationToken = default);
}
