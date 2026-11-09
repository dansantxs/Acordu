namespace Acordu.Application.Notificacoes;

/// <summary>Categorias dos avisos internos (RF-NOT-02/03/04): o sino recebe todas; o e-mail, as que a pessoa não desligou.</summary>
public enum CategoriaAviso
{
    NovaSolicitacao = 1,
    Atribuicao = 2,
    Sla = 3,
    Consumo = 4,

    /// <summary>Alguém mencionou a pessoa numa interação interna ou num comentário de tarefa (RF-COL-02).</summary>
    Mencao = 5,

    /// <summary>
    /// Novidade no que a pessoa segue: interação, comentário ou mudança de status (RF-COL-03). E-mail desligado por padrão
    /// (<see cref="PadroesDeAviso.EmailDesligadoPorPadrao"/>).
    /// </summary>
    Seguidas = 6,

    /// <summary>Cronômetro que ficou ligado além do expediente ou de 10 h (F12-18).</summary>
    Cronometro = 7,
}
