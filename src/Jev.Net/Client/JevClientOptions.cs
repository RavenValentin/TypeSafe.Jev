using Microsoft.Extensions.Logging;

namespace Jev.Net;

/// <summary>Settings for <see cref="JevClient"/>. Defaults mirror the official TypeSafe SDKs.</summary>
public sealed class JevClientOptions
{
    /// <summary>API key; falls back to the <c>TYPESAFE_API_KEY</c> environment variable.</summary>
    public string? ApiKey { get; set; } = Env("TYPESAFE_API_KEY");

    /// <summary>API root; falls back to <c>TYPESAFE_BASE_URL</c>, then <c>https://api.typesafe.ai</c>.</summary>
    public Uri BaseAddress { get; set; } = new(Env("TYPESAFE_BASE_URL") ?? "https://api.typesafe.ai");

    /// <summary>Model used when a call does not name one; falls back to <c>TYPESAFE_DEFAULT_MODEL</c>, then <c>jev-latest</c>.</summary>
    public string DefaultModel { get; set; } = Env("TYPESAFE_DEFAULT_MODEL") ?? "jev-latest";

    /// <summary>Timeout per attempt, not a budget for the whole call — that is <see cref="RetryPolicy.Budget"/>.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>When and how failed attempts are retried.</summary>
    public RetryPolicy Retry { get; set; } = RetryPolicy.Default;

    /// <summary>Headers added to every request. Protected headers cannot be replaced.</summary>
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; set; }

    /// <summary>Where the client logs. Without one it does not log at all.</summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>Clock and delay source for retries. Tests pass a fake; production leaves it alone.</summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>Dispose an <see cref="HttpClient"/> the caller supplied. Leave <c>false</c> for one from <c>IHttpClientFactory</c>.</summary>
    public bool DisposeHttpClient { get; set; }

    /// <summary>Blank environment variables count as unset.</summary>
    private static string? Env(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value && !string.IsNullOrWhiteSpace(value) ? value : null;

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new JevException("API key is missing. Set JevClientOptions.ApiKey or the TYPESAFE_API_KEY environment variable.");
        if (Timeout <= TimeSpan.Zero && Timeout != System.Threading.Timeout.InfiniteTimeSpan)
            throw new JevException("JevClientOptions.Timeout must be positive, or Timeout.InfiniteTimeSpan.");
        Retry.Validate();
    }
}
