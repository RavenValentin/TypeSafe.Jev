using System.Text.Json.Nodes;

namespace Jev.Net;

/// <summary>Yes/no question, answered with a probability from 0 to 1.</summary>
public sealed record NoulQuestion : Question
{
    /// <summary>Optional definitions of what "true" and "false" mean.</summary>
    public NoulCriteria? Criteria { get; init; }
}

/// <summary>Boundaries for a <see cref="NoulQuestion"/>. Each side accepts text or JSON structure.</summary>
public sealed record NoulCriteria
{
    /// <summary>What counts as yes/true.</summary>
    public JsonNode? True { get; init; }

    /// <summary>What counts as no/false.</summary>
    public JsonNode? False { get; init; }
}
