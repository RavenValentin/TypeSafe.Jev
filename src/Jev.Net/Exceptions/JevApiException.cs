using System.Net;

namespace Jev.Net;

/// <summary>The API answered with a non-success status.</summary>
public class JevApiException(HttpStatusCode status, string? body, string? requestId, string? message = null)
    : JevException(message ?? $"Jev API returned {(int)status} {status}.")
{
    /// <summary>HTTP status code of the response.</summary>
    public HttpStatusCode Status { get; } = status;

    /// <summary>Raw response body, if any. The API does not document an error body schema.</summary>
    public string? Body { get; } = body;

    /// <summary>Value of <c>x-typesafe-request-id</c>, if present.</summary>
    public string? RequestId { get; } = requestId;

    /// <summary>Creates the most specific exception type for <paramref name="status"/>.</summary>
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
