using System.Net;

namespace TypeSafe.Jev;

/// <summary>400: the request was malformed.</summary>
public sealed class JevBadRequestException(HttpStatusCode status, string? body, string? requestId, string? endpoint = null)
    : JevApiException(status, body, requestId, endpoint);

/// <summary>401: the API key is missing or invalid.</summary>
public sealed class JevAuthenticationException(HttpStatusCode status, string? body, string? requestId, string? endpoint = null)
    : JevApiException(status, body, requestId, endpoint);

/// <summary>403: the key is valid but not allowed to do this.</summary>
public sealed class JevPermissionDeniedException(HttpStatusCode status, string? body, string? requestId, string? endpoint = null)
    : JevApiException(status, body, requestId, endpoint);

/// <summary>404: unknown endpoint or resource.</summary>
public sealed class JevNotFoundException(HttpStatusCode status, string? body, string? requestId, string? endpoint = null)
    : JevApiException(status, body, requestId, endpoint);

/// <summary>422: request validation failed, e.g. too many choice options or too few score levels.</summary>
public sealed class JevUnprocessableEntityException(HttpStatusCode status, string? body, string? requestId, string? endpoint = null)
    : JevApiException(status, body, requestId, endpoint);

/// <summary>5xx, including 529 (service overloaded). Retried by default.</summary>
public sealed class JevInternalServerException(HttpStatusCode status, string? body, string? requestId, string? endpoint = null)
    : JevApiException(status, body, requestId, endpoint);

/// <summary>429: rate limit exceeded. Retried by default.</summary>
public sealed class JevRateLimitException(
    HttpStatusCode status, string? body, string? requestId, string? endpoint = null, TimeSpan? retryAfter = null)
    : JevApiException(status, body, requestId, endpoint)
{
    /// <summary>Server-suggested wait before retrying, from <c>Retry-After</c> or <c>retry-after-ms</c>.</summary>
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
