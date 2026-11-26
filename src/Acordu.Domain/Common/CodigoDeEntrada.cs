using System.Security.Cryptography;

namespace Acordu.Domain.Common;

/// <summary>
/// Código do endereço de entrada de uma empresa (design do F15, D2; ADR 0016, item 3): 8 caracteres num alfabeto minúsculo
/// sem os ambíguos (sem 0, 1, i, l, o), ~39 bits, sorteado com <see cref="RandomNumberGenerator"/> e não derivado do nome.
/// É identificador de roteamento, não credencial: fica em claro, com índice único entre todas as empresas.
/// </summary>
public static class CodigoDeEntrada
{
    /// <summary>31 símbolos: dígitos de 2 a 9 e as letras sem i, l e o.</summary>
    public const string Alfabeto = "23456789abcdefghjkmnpqrstuvwxyz";

    public const int Tamanho = 8;

    /// <summary>Um código novo, uniforme no alfabeto (sem viés de módulo).</summary>
    public static string Sortear() => RandomNumberGenerator.GetString(Alfabeto, Tamanho);

    /// <summary>Tem o tamanho e só símbolos do alfabeto (a consulta do destino, no F15d, recusa o resto sem ir ao banco).</summary>
    public static bool Valido(string? codigo) =>
        codigo is { Length: Tamanho } && !codigo.AsSpan().ContainsAnyExcept(Alfabeto.AsSpan());
}
