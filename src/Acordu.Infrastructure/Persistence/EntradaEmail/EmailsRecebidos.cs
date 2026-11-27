using Acordu.Application.EntradaEmail;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;
using Acordu.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Acordu.Infrastructure.Persistence.EntradaEmail;

/// <summary>Registro de entrada no tenant do contexto, sob os filtros (design do F15, D6).</summary>
public sealed class EmailsRecebidos : IEmailsRecebidos
{
    private readonly AcorduDbContext _db;

    public EmailsRecebidos(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public Task<bool> ExisteAsync(string chaveEntrada, CancellationToken cancellationToken = default) =>
        _db.EmailsRecebidos.AnyAsync(e => e.ChaveEntrada == chaveEntrada, cancellationToken);

    public Task<AnexosRecusados?> RecusadosDaAberturaAsync(Guid solicitacaoId, CancellationToken cancellationToken = default) =>
        _db.EmailsRecebidos.AsNoTracking()
            // A resposta na conversa (MENSAGEM, F15i) também aponta a solicitação; os recusados são só os da abertura.
            .Where(e => e.SolicitacaoId == solicitacaoId && e.Resultado == ResultadoEntradaEmail.Solicitacao)
            .OrderBy(e => e.RecebidoEm)
            .Select(e => e.Recusados)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// A gravação esbarrou no índice único da chave: outra entrega igual gravou entre a conferência e o commit (a corrida
    /// de duas repetições do servidor de origem). É repetição, não erro.
    /// </summary>
    public static bool ChaveRepetida(DbUpdateException excecao)
    {
        ArgumentNullException.ThrowIfNull(excecao);
        return excecao.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: EmailRecebidoConfiguration.IndiceChave,
        };
    }
}
