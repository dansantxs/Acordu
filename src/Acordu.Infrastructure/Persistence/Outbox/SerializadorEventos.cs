using System.Text.Json;
using System.Text.Json.Serialization;
using Acordu.Domain.Common;
using Acordu.Domain.ValueObjects;

namespace Acordu.Infrastructure.Persistence.Outbox;

/// <summary>
/// Eventos de domínio ↔ JSON da outbox. O tipo é o nome curto do record, resolvido numa lista fechada (só tipos de
/// <c>IEventoDominio</c> do assembly do Domain). Enums em SCREAMING_SNAKE e durações em minutos, como no contrato da API.
/// </summary>
public static class SerializadorEventos
{
    private static readonly Dictionary<string, Type> Tipos = typeof(IEventoDominio).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IEventoDominio).IsAssignableFrom(t))
        .ToDictionary(t => t.Name, StringComparer.Ordinal);

    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper),
            new DuracaoJsonConverter(),
            new DuracaoAssinadaJsonConverter(),
        },
    };

    public static string Tipo(IEventoDominio evento)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return evento.GetType().Name;
    }

    public static string Serializar(IEventoDominio evento)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return JsonSerializer.Serialize(evento, evento.GetType(), Opcoes);
    }

    public static IEventoDominio Desserializar(string tipo, string conteudo)
    {
        if (!Tipos.TryGetValue(tipo, out var clrType))
            throw new InvalidOperationException($"Tipo de evento desconhecido na outbox: '{tipo}'.");

        return (IEventoDominio)(JsonSerializer.Deserialize(conteudo, clrType, Opcoes)
            ?? throw new InvalidOperationException($"Evento '{tipo}' vazio na outbox."));
    }

    private sealed class DuracaoJsonConverter : JsonConverter<Duracao>
    {
        public override Duracao Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            Duracao.FromMinutos(reader.GetInt32());

        public override void Write(Utf8JsonWriter writer, Duracao value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(value.Minutos);
    }

    private sealed class DuracaoAssinadaJsonConverter : JsonConverter<DuracaoAssinada>
    {
        public override DuracaoAssinada Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(reader.GetInt32());

        public override void Write(Utf8JsonWriter writer, DuracaoAssinada value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(value.Minutos);
    }
}
