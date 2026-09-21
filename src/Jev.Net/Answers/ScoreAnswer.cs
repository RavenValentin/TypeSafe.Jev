using System.Text.Json.Nodes;

namespace Jev.Net;

/// <summary>Answer to a <see cref="ScoreQuestion"/>.</summary>
public sealed record ScoreAnswer : Answer
{
    /// <summary>Probability-weighted expected score; may fall between integer levels.</summary>
    public required double Score { get; init; }

    /// <summary>Confidence in <see cref="Score"/>, from 0 to 1.</summary>
    public required double Confidence { get; init; }

    /// <summary>The rubric echoed back, keyed by score level.</summary>
    public required IReadOnlyDictionary<int, JsonNode?> Legend { get; init; }

    /// <summary>Probability of every level, keyed by score level.</summary>
    public required IReadOnlyDictionary<int, double> Probabilities { get; init; }
}
