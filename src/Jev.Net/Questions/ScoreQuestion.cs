using System.Text.Json.Nodes;

namespace Jev.Net;

/// <summary>Ordered-scale question; the answer is a probability-weighted expected score.</summary>
public sealed record ScoreQuestion : Question
{
    /// <summary>Level descriptions (2–10), lowest first; the index is the score.</summary>
    public required IReadOnlyList<JsonNode?> Criteria { get; init; }
}
