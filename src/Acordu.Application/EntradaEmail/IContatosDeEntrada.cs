namespace Acordu.Application.EntradaEmail;

/// <summary>
/// Os contatos da empresa do contexto com um e-mail (design do F15, §1.2, "Identificação"), sob os filtros globais: a
/// identificação do remetente procura entre eles (F15-10, F15-24, F15-25). Ativos e inativos, de clientes ativos ou não;
/// a regra fica em <see cref="IdentificacaoDoRemetente"/>.
/// </summary>
public interface IContatosDeEntrada
{
    /// <param name="email">Já normalizado como o e-mail dos contatos.</param>
    Task<IReadOnlyList<ContatoDeEntrada>> PorEmailAsync(string email, CancellationToken cancellationToken = default);
}

/// <param name="ClienteId">O cliente do contato.</param>
/// <param name="ContatoId">O contato.</param>
/// <param name="UsuarioId">A conta do portal do contato, se ele tiver (o autor das mensagens dele, como no portal).</param>
/// <param name="ClienteAtivo">O cliente está ativo.</param>
/// <param name="ContatoAtivo">O contato está ativo.</param>
public sealed record ContatoDeEntrada(Guid ClienteId, Guid ContatoId, Guid? UsuarioId, bool ClienteAtivo, bool ContatoAtivo);
