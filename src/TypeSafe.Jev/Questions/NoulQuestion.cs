using System.Text.Json;

namespace TypeSafe.Jev;

/// <summary>Yes/no question, answered with a probability from 0 to 1.</summary>
public sealed record NoulQuestion : Question
{
    /// <summary>Optional definitions of what "true" and "false" mean.</summary>
    public NoulCriteria? Criteria { get; init; }

    internal override void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "noul");
        Instructions.WriteTo(writer, "instructions");
        if (Criteria is { } c)
        {
            writer.WritePropertyName("criteria");
            writer.WriteStartObject();
            c.True.WriteTo(writer, "true");
            c.False.WriteTo(writer, "false");
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }
}

/// <summary>Boundaries for a <see cref="NoulQuestion"/>. Each side takes text or JSON structure.</summary>
public sealed record NoulCriteria
{
    /// <summary>What counts as yes/true.</summary>
    public JsonContent True { get; init; }

    /// <summary>What counts as no/false.</summary>
    public JsonContent False { get; init; }
}
