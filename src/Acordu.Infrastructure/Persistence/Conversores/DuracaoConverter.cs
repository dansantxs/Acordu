using Acordu.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Acordu.Infrastructure.Persistence.Conversores;

/// <summary><see cref="Duracao"/> ↔ <c>integer</c> de minutos (ADR 0004). Colunas com sufixo <c>_minutos</c>.</summary>
public sealed class DuracaoConverter : ValueConverter<Duracao, int>
{
    public DuracaoConverter()
        : base(d => d.Minutos, m => Duracao.FromMinutos(m))
    {
    }
}

/// <summary><see cref="DuracaoAssinada"/> ↔ <c>integer</c> de minutos, com sinal.</summary>
public sealed class DuracaoAssinadaConverter : ValueConverter<DuracaoAssinada, int>
{
    public DuracaoAssinadaConverter()
        : base(d => d.Minutos, m => new DuracaoAssinada(m))
    {
    }
}
