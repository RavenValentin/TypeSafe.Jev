using System.Text.Json.Nodes;

namespace TypeSafe.Jev;

/// <summary>Per-call overrides. Anything left unset falls back to the client's <see cref="JevClientOptions"/>.</summary>
public sealed record JevRequestOptions
{
    /// <summary>Model for this call only, e.g. a pinned <c>jev-1.13.0</c>.</summary>
    public string? Model { get; init; }

    /// <summary>Retry rules for this call only.</summary>
    public RetryPolicy? Retry { get; init; }

    /// <summary>Per-attempt timeout for this call only.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Extra request headers. Protected headers cannot be replaced.</summary>
    public IReadOnlyDictionary<string, string>? ExtraHeaders { get; init; }

    /// <summary>
    /// Extra top-level fields merged into the request body — for parameters the API gains before this SDK
    /// models them. They cannot overwrite <c>state</c>, <c>model</c> or <c>questions</c>.
    /// </summary>
    public IReadOnlyDictionary<string, JsonNode?>? ExtraBody { get; init; }
}
