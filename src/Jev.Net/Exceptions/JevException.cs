namespace Jev.Net;

/// <summary>Base type for every failure raised by Jev.Net.</summary>
public class JevException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The request failed without an HTTP response (DNS, TLS, socket, interrupted body).</summary>
public sealed class JevConnectionException(Exception inner)
    : JevException("Request to the Jev API failed without a response.", inner);

/// <summary>A single attempt exceeded <see cref="JevClientOptions.Timeout"/>.</summary>
public sealed class JevTimeoutException(TimeSpan timeout, Exception? inner = null)
    : JevException($"Request to the Jev API timed out after {timeout}.", inner)
{
    /// <summary>The configured per-attempt timeout.</summary>
    public TimeSpan Timeout { get; } = timeout;
}

/// <summary>A successful response had an unexpected shape.</summary>
public sealed class JevResponseValidationException(string message, string? fieldPath = null, Exception? inner = null)
    : JevException(message, inner)
{
    /// <summary>Dotted path to the offending field, e.g. <c>answers.tone.confidence</c>.</summary>
    public string? FieldPath { get; } = fieldPath;
}
