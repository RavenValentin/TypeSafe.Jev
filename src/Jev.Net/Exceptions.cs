using System.Net;

namespace Jev.Net;

/// <summary>Base type for every failure raised by Jev.Net.</summary>
public class JevException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The API answered with a non-success status.</summary>
public class JevApiException(HttpStatusCode status, string? body, string? requestId, string? message = null)
    : JevException(message ?? $"Jev API returned {(int)status} {status}.")
{
    /// <summary>HTTP status code.</summary>
    public HttpStatusCode Status { get; } = status;
    /// <summary>Raw response body, if any.</summary>
    public string? Body { get; } = body;
    /// <summary>Value of <c>x-typesafe-request-id</c>, if present.</summary>
    public string? RequestId { get; } = requestId;

    internal static JevApiException From(HttpStatusCode status, string? body, string? requestId, TimeSpan? retryAfter) =>
        (int)status switch
        {
            400 => new JevBadRequestException(status, body, requestId),
            401 => new JevAuthenticationException(status, body, requestId),
            403 => new JevPermissionDeniedException(status, body, requestId),
            404 => new JevNotFoundException(status, body, requestId),
            422 => new JevUnprocessableEntityException(status, body, requestId),
            429 => new JevRateLimitException(status, body, requestId, retryAfter),
            >= 500 => new JevInternalServerException(status, body, requestId),
            _ => new JevApiException(status, body, requestId),
        };
}

/// <summary>400.</summary>
public sealed class JevBadRequestException(HttpStatusCode s, string? b, string? r) : JevApiException(s, b, r);
/// <summary>401: missing or invalid API key.</summary>
public sealed class JevAuthenticationException(HttpStatusCode s, string? b, string? r) : JevApiException(s, b, r);
/// <summary>403.</summary>
public sealed class JevPermissionDeniedException(HttpStatusCode s, string? b, string? r) : JevApiException(s, b, r);
/// <summary>404.</summary>
public sealed class JevNotFoundException(HttpStatusCode s, string? b, string? r) : JevApiException(s, b, r);
/// <summary>422: request validation failed.</summary>
public sealed class JevUnprocessableEntityException(HttpStatusCode s, string? b, string? r) : JevApiException(s, b, r);
/// <summary>5xx, including 529 (overloaded).</summary>
public sealed class JevInternalServerException(HttpStatusCode s, string? b, string? r) : JevApiException(s, b, r);

/// <summary>429: rate limit exceeded.</summary>
public sealed class JevRateLimitException(HttpStatusCode s, string? b, string? r, TimeSpan? retryAfter) : JevApiException(s, b, r)
{
    /// <summary>Server-suggested wait before retrying, from <c>Retry-After</c> / <c>retry-after-ms</c>.</summary>
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

/// <summary>The request failed without an HTTP response.</summary>
public sealed class JevConnectionException(Exception inner) : JevException("Request to Jev API failed without a response.", inner);

/// <summary>A single attempt exceeded <see cref="JevClientOptions.Timeout"/>.</summary>
public sealed class JevTimeoutException(TimeSpan timeout, Exception? inner = null)
    : JevException($"Request to Jev API timed out after {timeout}.", inner)
{
    /// <summary>The configured per-attempt timeout.</summary>
    public TimeSpan Timeout { get; } = timeout;
}

/// <summary>A 2xx response had an unexpected shape.</summary>
public sealed class JevResponseValidationException(string message, string? fieldPath = null, Exception? inner = null)
    : JevException(message, inner)
{
    /// <summary>Dotted path to the offending field, e.g. <c>answers.tone.confidence</c>.</summary>
    public string? FieldPath { get; } = fieldPath;
}
