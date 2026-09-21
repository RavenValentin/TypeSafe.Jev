using System.Text.Json;

namespace TypeSafe.Jev;

/// <summary>Single-selection question over a set of named options.</summary>
public sealed record ChoiceQuestion : Question
{
    private readonly IReadOnlyDictionary<string, JsonContent> _criteria = null!;

    /// <summary>Options keyed by name; the value is a description, a JSON rubric, or unset.</summary>
    /// <remarks>The API accepts at most 255 options and rejects an out-of-range question with a 422.</remarks>
    /// <exception cref="ArgumentException">The set is empty.</exception>
    public required IReadOnlyDictionary<string, JsonContent> Criteria
    {
        get => _criteria;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Count == 0) throw new ArgumentException("A choice needs at least one option.", nameof(Criteria));
            _criteria = value;
        }
    }

    internal override void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "choice");
        Instructions.WriteTo(writer, "instructions");

        writer.WritePropertyName("criteria");
        writer.WriteStartObject();
        foreach (var (name, description) in Criteria)
        {
            // An option with no description is sent as an explicit null: the API reads it by its name.
            writer.WritePropertyName(name);
            if (description.HasValue) description.WriteTo(writer);
            else writer.WriteNullValue();
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
