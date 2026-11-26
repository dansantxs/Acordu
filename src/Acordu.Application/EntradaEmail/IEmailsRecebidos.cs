namespace Acordu.Application.EntradaEmail;

/// <summary>
/// Registro de entrada (<see cref="Domain.Entities.EmailRecebido"/>; design do F15, D6), no tenant do contexto e sob os
/// filtros globais. A gravação é pelo repositório e pela unidade de trabalho.
/// </summary>
public interface IEmailsRecebidos
{
    /// <summary>A empresa já registrou um e-mail com esta chave (repetição da mesma entrega; F15-23, F15-97, F15-103).</summary>
    Task<bool> ExisteAsync(string chaveEntrada, CancellationToken cancellationToken = default);
}
