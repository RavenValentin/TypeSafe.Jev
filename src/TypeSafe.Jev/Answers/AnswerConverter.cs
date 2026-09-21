using System.Text.Json;
using System.Text.Json.Serialization;

namespace TypeSafe.Jev;

/// <summary>
/// Reads the <c>type</c> discriminator regardless of property order and dispatches to the concrete answer
/// type. It goes through the source-generated metadata rather than reflection, so it survives trimming.
/// </summary>
internal sealed class AnswerConverter : JsonConverter<Answer>
{
    public override Answer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var type = document.RootElement.TryGetProperty("type", out var discriminator) ? discriminator.GetString() : null;

        return type switch
        {
            "noul" => document.RootElement.Deserialize(JevJsonContext.Default.NoulAnswer)!,
            "choice" => document.RootElement.Deserialize(JevJsonContext.Default.ChoiceAnswer)!,
            "score" => document.RootElement.Deserialize(JevJsonContext.Default.ScoreAnswer)!,
            null => throw new JevResponseValidationException("An answer has no 'type'.", "type"),
            _ => throw new JevResponseValidationException($"Unknown answer type '{type}'.", "type"),
        };
    }

    public override void Write(Utf8JsonWriter writer, Answer value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case NoulAnswer noul:
                JsonSerializer.Serialize(writer, noul, JevJsonContext.Default.NoulAnswer);
                break;
            case ChoiceAnswer choice:
                JsonSerializer.Serialize(writer, choice, JevJsonContext.Default.ChoiceAnswer);
                break;
            case ScoreAnswer score:
                JsonSerializer.Serialize(writer, score, JevJsonContext.Default.ScoreAnswer);
                break;
            default:
                throw new JsonException($"Cannot write answer type {value.GetType().Name}.");
        }
    }
}
