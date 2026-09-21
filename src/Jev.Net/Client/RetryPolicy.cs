using System.Net;

namespace Jev.Net;

/// <summary>
/// When and how a failed attempt is retried. Defaults mirror the official TypeSafe SDKs, so behaviour
/// matches what their docs describe.
/// </summary>
public sealed record RetryPolicy
{
    /// <summary>Statuses retried by default: 408, 429 and every 5xx.</summary>
    public static IReadOnlySet<int> DefaultHttpStatuses { get; } =
        new HashSet<int>([408, 429, .. Enumerable.Range(500, 100)]);

    /// <summary>The defaults: 2 retries, 0.5s–5s backoff, <c>Retry-After</c> honoured, a 30s budget.</summary>
    public static RetryPolicy Default { get; } = new();

    /// <summary>Never retries.</summary>
    public static RetryPolicy None { get; } = new() { MaxRetries = 0 };

    /// <summary>Attempts after the first; <c>0</c> disables retrying.</summary>
    public int MaxRetries { get; init; } = 2;

    /// <summary>First backoff delay, doubled after every attempt up to <see cref="BackoffMax"/>.</summary>
    public TimeSpan BackoffInitial { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Ceiling for the doubling backoff.</summary>
    public TimeSpan BackoffMax { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Fraction of each delay randomly subtracted, from 0 to 1, so retries do not synchronize.</summary>
    public double BackoffJitter { get; init; } = 0.25;

    /// <summary>HTTP statuses worth another attempt.</summary>
    public IReadOnlySet<int> HttpStatuses { get; init; } = DefaultHttpStatuses;

    /// <summary>Honour the server's <c>Retry-After</c> / <c>retry-after-ms</c> instead of the computed backoff.</summary>
    public bool RespectRetryAfter { get; init; } = true;

    /// <summary>Longest server-requested wait honoured; anything longer falls back to backoff.</summary>
    public TimeSpan MaxRetryAfter { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Retry a request that never got an HTTP response.</summary>
    public bool RetryConnectionErrors { get; init; } = true;

    /// <summary>Retry an attempt that hit the per-attempt timeout.</summary>
    public bool RetryTimeouts { get; init; } = true;

    /// <summary>
    /// Total time one call may spend, waits included. A retry whose wait would exceed it is not taken and the
    /// last failure is rethrown, so a call cannot hang for minutes behind a long <c>Retry-After</c>.
    /// <c>null</c> removes the budget.
    /// </summary>
    public TimeSpan? Budget { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The last word on whether to retry a failure the rules above already accepted — return <c>false</c>
    /// to stop. Runs after them, never instead of them.
    /// </summary>
    public Func<JevException, bool>? Predicate { get; init; }

    /// <summary>Whether <paramref name="failure"/> is worth another attempt.</summary>
    internal bool ShouldRetry(JevException failure)
    {
        var retryable = failure switch
        {
            JevTimeoutException => RetryTimeouts,
            JevConnectionException => RetryConnectionErrors,
            JevApiException api => HttpStatuses.Contains((int)api.Status),
            _ => false,
        };

        return retryable && (Predicate is null || Predicate(failure));
    }

    /// <summary>How long to wait before attempt number <paramref name="attempt"/> + 1.</summary>
    internal TimeSpan Delay(int attempt, TimeSpan? retryAfter, Random random)
    {
        if (RespectRetryAfter && retryAfter is { } wait && wait <= MaxRetryAfter)
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;

        var doubled = BackoffInitial.TotalMilliseconds * Math.Pow(2, attempt);
        var capped = Math.Min(doubled, BackoffMax.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(capped * (1 - BackoffJitter * random.NextDouble()));
    }

    /// <summary>Validates the numbers a caller can get wrong.</summary>
    internal void Validate()
    {
        if (MaxRetries < 0) throw new JevException("RetryPolicy.MaxRetries must be zero or more.");
        if (BackoffJitter is < 0 or > 1) throw new JevException("RetryPolicy.BackoffJitter must be between 0 and 1.");
        if (BackoffInitial < TimeSpan.Zero) throw new JevException("RetryPolicy.BackoffInitial must not be negative.");
        if (BackoffMax < BackoffInitial) throw new JevException("RetryPolicy.BackoffMax must be at least BackoffInitial.");
        if (Budget is { } b && b <= TimeSpan.Zero) throw new JevException("RetryPolicy.Budget must be positive.");
    }

    /// <summary>Reads the server's requested wait from a response.</summary>
    internal static TimeSpan? RetryAfterOf(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("retry-after-ms", out var ms)
            && double.TryParse(ms.FirstOrDefault(), System.Globalization.CultureInfo.InvariantCulture, out var value))
            return TimeSpan.FromMilliseconds(value);

        var header = response.Headers.RetryAfter;
        return header?.Delta ?? (header?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
    }
}
