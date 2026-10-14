using Acordu.Domain.Common;
using Acordu.Domain.Enums;

namespace Acordu.Domain.Events;

/// <summary>
/// Pessoas da equipe mencionadas numa interação interna ou num comentário de tarefa (RF-COL-02). <see cref="AlvoId"/> é a
/// solicitação ou a tarefa; <see cref="OrigemId"/>, a interação ou o comentário com o texto.
/// </summary>
public sealed record PessoasMencionadas(
    Guid TenantId,
    AlvoColaboracao Alvo,
    Guid AlvoId,
    Guid OrigemId,
    Guid AutorId,
    IReadOnlyList<Guid> Mencionados) : IEventoDominio;
