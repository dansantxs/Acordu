using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Acordu.Domain.ValueObjects;

namespace Acordu.Api.Composicao;

/// <summary>Período mensal no formato do contrato da API: <c>yyyy-MM</c>.</summary>
internal sealed class MesReferenciaJsonConverter : JsonConverter<MesReferencia>
{
    public override MesReferencia Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var texto = reader.GetString();
        if (texto is null || !DateOnly.TryParseExact(texto + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
            throw new JsonException("Período inválido: use o formato yyyy-MM.");
        return MesReferencia.De(data);
    }

    public override void Write(Utf8JsonWriter writer, MesReferencia value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToString());
    }
}
