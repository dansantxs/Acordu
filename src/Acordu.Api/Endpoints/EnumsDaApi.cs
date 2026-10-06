using System.Text.Json;

namespace Acordu.Api.Endpoints;

/// <summary>Enums na query string no mesmo formato do JSON (<c>BANCO_HORAS</c>); o binding padrão só aceita o nome C#.</summary>
internal static class EnumsDaApi
{
    public static T? Ler<T>(string? valor, string parametro)
        where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;
        foreach (var opcao in Enum.GetValues<T>())
        {
            if (string.Equals(JsonNamingPolicy.SnakeCaseUpper.ConvertName(opcao.ToString()), valor.Trim(), StringComparison.OrdinalIgnoreCase))
                return opcao;
        }

        throw new BadHttpRequestException($"Valor inválido para {parametro}: {valor}.");
    }
}
