using System.Text.Json;

namespace TypeSafe.Jev;

/// <summary>Ordered-scale question; the answer is a probability-weighted expected score.</summary>
public sealed record ScoreQuestion : Question
{
    private readonly IReadOnlyList<JsonContent> _criteria = null!;

    /// <summary>Level descriptions, lowest first; the index is the score.</summary>
    /// <remarks>The API accepts 2–10 levels and rejects an out-of-range question with a 422.</remarks>
    /// <exception cref="ArgumentException">The rubric is empty.</exception>
    public required IReadOnlyList<JsonContent> Criteria
    {
        get => _criteria;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Count == 0) throw new ArgumentException("A score needs at least one level.", nameof(Criteria));
            _criteria = value;
        }
    }

    internal override void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "score");
        Instructions.WriteTo(writer, "instructions");

        writer.WritePropertyName("criteria");
        writer.WriteStartArray();
        foreach (var level in Criteria)
        {
            if (level.HasValue) level.WriteTo(writer);
            else writer.WriteNullValue();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
