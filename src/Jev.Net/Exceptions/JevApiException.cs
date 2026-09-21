using System.Net;
using System.Text.Json;

namespace Jev.Net;

/// <summary>The API answered with a non-success status.</summary>
public class JevApiException : JevException
{
    /// <summary>Creates an exception describing a failed call.</summary>
    public JevApiException(HttpStatusCode status, string? body, string? requestId, string? endpoint = null, string? message = null)
        : base(message ?? Describe(status, body, requestId, endpoint))
    {
        Status = status;
        Body = body;
        RequestId = requestId;
        Endpoint = endpoint;
    }

    /// <summary>HTTP status code of the response.</summary>
    public HttpStatusCode Status { get; }

    /// <summary>Raw response body, if any. The API does not document an error body schema.</summary>
    public string? Body { get; }

    /// <summary>Value of <c>x-typesafe-request-id</c>, if present. Quote it in support requests.</summary>
    public string? RequestId { get; }

    /// <summary>The request line, e.g. <c>POST https://api.typesafe.ai/v1/systemone</c>, never carrying credentials or a query.</summary>
    public string? Endpoint { get; }

    /// <summary>Creates the most specific exception type for <paramref name="status"/>.</summary>
    internal static JevApiException From(HttpStatusCode status, string? body, string? requestId, string? endpoint, TimeSpan? retryAfter) =>
        (int)status switch
        {
            400 => new JevBadRequestException(status, body, requestId, endpoint),
            401 => new JevAuthenticationException(status, body, requestId, endpoint),
            403 => new JevPermissionDeniedException(status, body, requestId, endpoint),
            404 => new JevNotFoundException(status, body, requestId, endpoint),
            422 => new JevUnprocessableEntityException(status, body, requestId, endpoint),
            429 => new JevRateLimitException(status, body, requestId, endpoint, retryAfter),
            >= 500 => new JevInternalServerException(status, body, requestId, endpoint),
            _ => new JevApiException(status, body, requestId, endpoint),
        };

    /// <summary><c>POST https://api.typesafe.ai/v1/systemone: 429 slow down (request_id=abc)</c>.</summary>
    private static string Describe(HttpStatusCode status, string? body, string? requestId, string? endpoint)
    {
        var where = endpoint is null ? "Jev API" : endpoint;
        var why = Explain(body) ?? status.ToString();
        var which = requestId is null ? "" : $" (request_id={requestId})";
        return $"{where}: {(int)status} {why}{which}";
    }

    /// <summary>Pulls the server's explanation out of the body when it looks like JSON, without assuming a schema.</summary>
    private static string? Explain(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        if (body.Length > 4096 || body[0] is not ('{' or '[')) return null;

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind is not JsonValueKind.Object) return null;

            foreach (var name in (ReadOnlySpan<string>)["message", "error", "detail", "description"])
            {
                if (!document.RootElement.TryGetProperty(name, out var value)) continue;
                if (value.ValueKind is JsonValueKind.String) return value.GetString();
                if (value.ValueKind is JsonValueKind.Object && value.TryGetProperty("message", out var nested)
                    && nested.ValueKind is JsonValueKind.String) return nested.GetString();
            }
        }
        catch (JsonException)
        {
            // Not JSON after all; the status alone will have to do.
        }

        return null;
    }
}
