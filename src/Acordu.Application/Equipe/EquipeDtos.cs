using Acordu.Application.Common;

namespace Acordu.Application.Equipe;

/// <summary>Pessoa da equipe no tenant (contrato §3). Contatos do portal ficam nos clientes, não aqui.</summary>
/// <remarks><see cref="CapacidadeSemanalMinutos"/>: horas por semana para o planejamento (padrão 40h).</remarks>
public sealed record UsuarioEquipeDto(
    Guid UsuarioId, string Nome, string Email, Referencia Papel, bool Dono, int CapacidadeSemanalMinutos);

/// <summary>Convite pendente; <see cref="ExpiraEm"/> é nulo até o e-mail sair.</summary>
public sealed record ConviteEquipeDto(Guid Id, string Email, Referencia Papel, DateTimeOffset CriadoEm, DateTimeOffset? ExpiraEm);

/// <summary>Corpo do <c>POST /convites</c>.</summary>
public sealed record ConviteEquipeRequest(string Email, Guid? PapelId);

/// <summary>Corpo do <c>PATCH /usuarios/{id}/papel</c>.</summary>
public sealed record TrocaPapelRequest(Guid? PapelId);

/// <summary>Corpo do <c>PUT /usuarios/{id}/capacidade</c>: de 0 a 10080 minutos (168h).</summary>
public sealed record CapacidadeRequest(int CapacidadeSemanalMinutos);

/// <summary>Leituras da gestão da equipe, pelos filtros do contexto.</summary>
public interface IConsultasEquipe
{
    /// <summary>Equipe (vínculos sem cliente), por nome.</summary>
    Task<IReadOnlyList<UsuarioEquipeDto>> UsuariosAsync(CancellationToken cancellationToken = default);

    /// <summary>Uma pessoa da equipe; nula se não é da equipe deste tenant.</summary>
    Task<UsuarioEquipeDto?> UsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Convites pendentes (nem aceitos nem revogados), dos mais novos.</summary>
    Task<IReadOnlyList<ConviteEquipeDto>> ConvitesPendentesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Convites pendentes, rastreados (reconvidar e excluir o papel revogam).</summary>
public interface IConvitesEquipe
{
    Task<IReadOnlyList<Domain.Entities.ConviteEquipe>> PendentesAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Pendentes com o papel (excluir o papel os revoga).</summary>
    Task<IReadOnlyList<Domain.Entities.ConviteEquipe>> PendentesDoPapelAsync(Guid papelId, CancellationToken cancellationToken = default);
}
