namespace Jev.Net;

/// <summary>Settings for <see cref="JevClient"/>. Defaults mirror the official TypeSafe SDKs.</summary>
public sealed class JevClientOptions
{
    /// <summary>API key; falls back to the <c>TYPESAFE_API_KEY</c> environment variable.</summary>
    public string? ApiKey { get; set; } = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");

    /// <summary>API root; falls back to <c>TYPESAFE_BASE_URL</c>, then <c>https://api.typesafe.ai</c>.</summary>
    public Uri BaseAddress { get; set; } =
        new(Environment.GetEnvironmentVariable("TYPESAFE_BASE_URL") ?? "https://api.typesafe.ai");

    /// <summary>Model used when a request does not name one; falls back to <c>TYPESAFE_DEFAULT_MODEL</c>, then <c>jev-latest</c>.</summary>
    public string DefaultModel { get; set; } = Environment.GetEnvironmentVariable("TYPESAFE_DEFAULT_MODEL") ?? "jev-latest";

    /// <summary>Timeout per attempt (not a total budget).</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Retries after the first attempt on 408/429/5xx, connection and timeout errors; 0 disables.</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>Largest server-provided <c>Retry-After</c> honored; longer values fall back to backoff.</summary>
    public TimeSpan MaxRetryAfter { get; set; } = TimeSpan.FromSeconds(60);

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new JevException("API key is missing. Set JevClientOptions.ApiKey or the TYPESAFE_API_KEY environment variable.");
        if (MaxRetries < 0) throw new JevException("MaxRetries must be >= 0.");
    }
}
