using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Jev.Net.Tests.Unit;

/// <summary>What the stub should answer with. A recipe, not a message: the client disposes every response
/// it reads, so a retry has to be handed a fresh one.</summary>
internal sealed record Reply(HttpStatusCode Status, string Body, params (string Name, string Value)[] Headers)
{
    public HttpResponseMessage ToResponse()
    {
        var response = new HttpResponseMessage(Status) { Content = new StringContent(Body, Encoding.UTF8, "application/json") };
        foreach (var (name, value) in Headers) response.Headers.TryAddWithoutValidation(name, value);
        return response;
    }
}

/// <summary>Replays a scripted sequence of replies and records every request it was given.</summary>
internal sealed class StubHandler(params Reply[] replies) : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    /// <summary>Thrown instead of answering, when set — for connection and timeout cases.</summary>
    public Func<int, Exception?>? Throw { get; init; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(
            request.Method.Method,
            request.RequestUri!.ToString(),
            request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase),
            body));

        if (Throw?.Invoke(Requests.Count - 1) is { } failure) throw failure;

        cancellationToken.ThrowIfCancellationRequested();
        return replies[Math.Min(Requests.Count - 1, replies.Length - 1)].ToResponse();
    }

    public static Reply Json(HttpStatusCode status, string body, params (string Name, string Value)[] headers) =>
        new(status, body, headers);
}

internal sealed record RecordedRequest(string Method, string Uri, Dictionary<string, string> Headers, string Body);

/// <summary>Captures log entries so tests can assert on what was written — and what was redacted.</summary>
internal sealed class RecordingLoggerFactory(LogLevel minimum = LogLevel.Debug) : ILoggerFactory
{
    public List<string> Entries { get; } = [];

    public ILogger CreateLogger(string categoryName) => new Recorder(this, minimum);

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() { }

    private sealed class Recorder(RecordingLoggerFactory owner, LogLevel minimum) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= minimum;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) owner.Entries.Add(formatter(state, exception));
        }
    }
}
