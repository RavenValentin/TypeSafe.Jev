namespace Jev.Net;

/// <summary>Token counts for one request.</summary>
public sealed record Usage
{
    /// <summary>Input tokens consumed; these are what the API bills.</summary>
    public int? InputTokens { get; init; }

    /// <summary>Output tokens produced.</summary>
    public int? OutputTokens { get; init; }
}
