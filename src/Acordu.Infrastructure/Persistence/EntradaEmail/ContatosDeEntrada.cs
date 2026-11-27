using Acordu.Application.EntradaEmail;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.EntradaEmail;

/// <summary>
/// Os contatos com um e-mail na empresa do contexto (identificação do remetente, design do F15 §1.2), sob os filtros
/// globais: só a empresa do destino aparece (F15-20, F15-21; ADR 0016, item 3).
/// </summary>
public sealed class ContatosDeEntrada : IContatosDeEntrada
{
    private readonly AcorduDbContext _db;

    public ContatosDeEntrada(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<IReadOnlyList<ContatoDeEntrada>> PorEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        // Os clientes com algum contato daquele e-mail (os contatos vêm junto, são do agregado); poucos, e o filtro do
        // contato fica na memória.
        var clientes = await _db.Clientes.AsNoTracking()
            .Where(c => c.Contatos.Any(x => x.Email == email))
            .ToListAsync(cancellationToken);
        return
        [
            .. clientes.SelectMany(c => c.Contatos
                .Where(x => x.Email == email)
                .Select(x => new ContatoDeEntrada(c.Id, x.Id, x.UsuarioId, c.Ativo, x.Ativo))),
        ];
    }
}
