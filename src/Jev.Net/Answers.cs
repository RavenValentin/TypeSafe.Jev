using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Jev.Net;

/// <summary>An answer from Jev; the concrete type matches the question type.</summary>
[JsonConverter(typeof(AnswerConverter))]
public abstract record Answer;

/// <summary>Answer to a <see cref="NoulQuestion"/>.</summary>
public sealed record NoulAnswer : Answer
{
    /// <summary>Probability of yes/true, from 0 to 1; near 0.5 means uncertain.</summary>
    public required double Noul { get; init; }
}

/// <summary>Answer to a <see cref="ChoiceQuestion"/>.</summary>
public sealed record ChoiceAnswer : Answer
{
    /// <summary>Name of the most probable option.</summary>
    public required string Choice { get; init; }
    /// <summary>Confidence in <see cref="Choice"/>, from 0 to 1.</summary>
    public required double Confidence { get; init; }
    /// <summary>Probability of every option, keyed by option name.</summary>
    public required IReadOnlyDictionary<string, double> Probabilities { get; init; }

    /// <summary>Maps <see cref="Choice"/> back to <typeparamref name="TEnum"/>.</summary>
    /// <exception cref="JevResponseValidationException">The chosen name is not a member of <typeparamref name="TEnum"/>.</exception>
    public TEnum As<TEnum>() where TEnum : struct, Enum =>
        EnumNames.FromWire<TEnum>(Choice)
        ?? throw new JevResponseValidationException($"Choice '{Choice}' is not a member of {typeof(TEnum).Name}.", "choice");
}

/// <summary>Answer to a <see cref="ScoreQuestion"/>.</summary>
public sealed record ScoreAnswer : Answer
{
    /// <summary>Probability-weighted expected score; may fall between integer levels.</summary>
    public required double Score { get; init; }
    /// <summary>Confidence in <see cref="Score"/>, from 0 to 1.</summary>
    public required double Confidence { get; init; }
    /// <summary>Level descriptions keyed by score.</summary>
    public required IReadOnlyDictionary<int, JsonNode?> Legend { get; init; }
    /// <summary>Probability of every level keyed by score.</summary>
    public required IReadOnlyDictionary<int, double> Probabilities { get; init; }
}

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
