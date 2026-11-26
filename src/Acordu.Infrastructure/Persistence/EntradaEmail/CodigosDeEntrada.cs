using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Acordu.Infrastructure.Persistence.EntradaEmail;

/// <summary>
/// Unicidade do código de entrada entre todas as empresas (design do F15, D2): o índice único <see cref="Indice"/> barra a
/// colisão, e o cadastro sorteia outro código e grava de novo (<see cref="Tentativas"/> vezes no máximo). Com ~39 bits, a
/// colisão é rara a ponto de a segunda tentativa praticamente nunca acontecer; o laço existe para ela não virar erro.
/// </summary>
public static class CodigosDeEntrada
{
    public const string Indice = "ix_tenants_codigo_entrada";

    public const int Tentativas = 5;

    /// <summary>A gravação esbarrou no índice único do código de entrada.</summary>
    public static bool Disputa(DbUpdateException excecao)
    {
        ArgumentNullException.ThrowIfNull(excecao);
        return excecao.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: Indice };
    }
}
