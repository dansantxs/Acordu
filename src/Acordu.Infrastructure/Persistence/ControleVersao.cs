using System.Globalization;
using Acordu.Application.Abstractions;
using Acordu.Domain.Common;
using Acordu.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence;

/// <summary>
/// <c>If-Match</c> sobre o <c>xmin</c>: a versão esperada vira o valor original do token de concorrência, e a raiz é
/// marcada como alterada para que o UPDATE (com <c>WHERE xmin = esperada</c>) aconteça mesmo quando só uma entidade filha
/// mudou. Divergência → <see cref="DbUpdateConcurrencyException"/> no save → 409.
/// </summary>
public sealed class ControleVersao : IControleVersao
{
    private readonly AcorduDbContext _db;

    public ControleVersao(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public string Versao(EntidadeBase raiz)
    {
        ArgumentNullException.ThrowIfNull(raiz);
        var versao = (uint)_db.Entry(raiz).Property(ConfiguracaoRaiz.Versao).CurrentValue!;
        return versao.ToString(CultureInfo.InvariantCulture);
    }

    public void Esperar(EntidadeBase raiz, string? versaoEsperada)
    {
        ArgumentNullException.ThrowIfNull(raiz);
        if (versaoEsperada is null)
            return;
        if (!uint.TryParse(versaoEsperada, NumberStyles.None, CultureInfo.InvariantCulture, out var versao))
            throw new ArgumentException("If-Match deve trazer a versão recebida no recurso.", nameof(versaoEsperada));

        var entrada = _db.Entry(raiz);
        entrada.Property(ConfiguracaoRaiz.Versao).OriginalValue = versao;
        if (entrada.State == EntityState.Unchanged)
            entrada.State = EntityState.Modified;
    }
}
