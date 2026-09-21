using Microsoft.Extensions.Logging;

namespace Jev.Net;

/// <summary>
/// One Information line per attempt; headers and bodies at Debug. Credentials are redacted —
/// request and response <em>bodies</em> are not, because they are whatever state you sent.
/// Do not enable Debug where that matters.
/// </summary>
internal sealed class Log(ILogger logger)
{
    private static readonly string[] SecretMarkers = ["authorization", "token", "secret", "key", "cookie"];

    public bool IsDebug => logger.IsEnabled(LogLevel.Debug);

    public void Attempt(HttpMethod method, Uri uri, int attempt)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("{Method} {Uri} (attempt {Attempt})", method, Sanitize(uri), attempt + 1);
    }

    public void Completed(int status, string? requestId, double seconds)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("-> {Status} in {Seconds:0.000}s (request {RequestId})", status, seconds, requestId ?? "-");
    }

    public void Retrying(JevException failure, TimeSpan delay)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("retrying in {Delay} after {Failure}", delay, failure.Message);
    }

    public void Failed(JevException failure)
    {
        if (logger.IsEnabled(LogLevel.Warning))
            logger.LogWarning("giving up: {Failure}", failure.Message);
    }

    public void Headers(string direction, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
    {
        if (!IsDebug) return;
        foreach (var (name, values) in headers)
            logger.LogDebug("{Direction} {Header}: {Value}", direction, name, Redact(name, values));
    }

    public void Body(string direction, string body)
    {
        if (IsDebug) logger.LogDebug("{Direction} body: {Body}", direction, body);
    }

    /// <summary>Anything that looks like a credential is replaced, not truncated.</summary>
    private static string Redact(string name, IEnumerable<string> values) =>
        SecretMarkers.Any(marker => name.Contains(marker, StringComparison.OrdinalIgnoreCase))
            ? "<redacted>"
            : string.Join(", ", values);

    /// <summary>A URI never reaches a log with credentials or a query string on it.</summary>
    private static string Sanitize(Uri uri) => new UriBuilder(uri) { UserName = "", Password = "", Query = "", Fragment = "" }.Uri.ToString();
}
