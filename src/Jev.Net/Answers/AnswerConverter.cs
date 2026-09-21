using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jev.Net;

/// <summary>Reads the <c>type</c> discriminator regardless of property order and dispatches to the concrete answer type.</summary>
internal sealed class AnswerConverter : JsonConverter<Answer>
{
    public override Answer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var type = doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
        return type switch
        {
            "noul" => doc.RootElement.Deserialize<NoulAnswer>(options)!,
            "choice" => doc.RootElement.Deserialize<ChoiceAnswer>(options)!,
            "score" => doc.RootElement.Deserialize<ScoreAnswer>(options)!,
            _ => throw new JevResponseValidationException($"Unknown answer type '{type}'.", "type"),
        };
    }

    public override void Write(Utf8JsonWriter writer, Answer value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
}
