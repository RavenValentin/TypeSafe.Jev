using System.Text.Json.Nodes;

namespace Jev.Net;

/// <summary>Single-selection question over a set of named options.</summary>
public sealed record ChoiceQuestion : Question
{
    /// <summary>Options keyed by name (1–255); each value is a description, JSON rubric, or <c>null</c>.</summary>
    public required IReadOnlyDictionary<string, JsonNode?> Criteria { get; init; }
}
