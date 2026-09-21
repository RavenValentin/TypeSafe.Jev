using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jev.Net;

/// <summary>
/// Default <see cref="IJevClient"/> over <see cref="HttpClient"/>. Thread-safe and cheap to construct;
/// keep one for the lifetime of your app.
/// </summary>
public sealed class JevClient : IJevClient, IDisposable
{
    private const string SystemOnePath = "v1/systemone";
    private const string ModelsPath = "v1/models";

    private readonly HttpClient _http;
    private readonly JevClientOptions _options;
    private readonly Log _log;
    private readonly bool _disposeHttp;

    /// <summary>Uses a caller-supplied <see cref="HttpClient"/>, e.g. one from <c>IHttpClientFactory</c>.</summary>
    /// <remarks>Set <see cref="JevClientOptions.DisposeHttpClient"/> to have this client dispose it.</remarks>
    public JevClient(HttpClient httpClient, JevClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        ValidateHeaders(options.DefaultHeaders, nameof(JevClientOptions.DefaultHeaders));

        _http = httpClient;
        _options = options;
        _disposeHttp = options.DisposeHttpClient;
        _log = new Log((options.LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger("Jev.Net"));
    }

    /// <summary>Creates a client that owns its own <see cref="HttpClient"/>.</summary>
    public JevClient(JevClientOptions? options = null)
        : this(new HttpClient(), Own(options ?? new JevClientOptions())) { }

    /// <summary>Creates a client for <paramref name="apiKey"/> with default settings.</summary>
    public JevClient(string apiKey) : this(new JevClientOptions { ApiKey = apiKey }) { }

    private static JevClientOptions Own(JevClientOptions options)
    {
        options.DisposeHttpClient = true;
        return options;
    }

    /// <inheritdoc />
    public async Task<SystemOneResponse> SystemOneAsync(
        JsonContent state,
        IReadOnlyDictionary<string, Question> questions,
        JevRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(questions);
        if (questions.Count == 0) throw new ArgumentException("At least one question is required.", nameof(questions));
        foreach (var (name, question) in questions)
            if (question is null) throw new ArgumentException($"Question '{name}' is null.", nameof(questions));

        var model = options?.Model ?? _options.DefaultModel;
        var body = WriteRequest(state, questions, model, options?.ExtraBody);

        using var activity = StartActivity("system_one", model);
        var response = await SendAsync(
            HttpMethod.Post, SystemOnePath, body, options, activity, "system_one", cancellationToken).ConfigureAwait(false);

        using (response)
        {
            var result = await ReadAsync(response, JevJsonContext.Default.SystemOneResponse, cancellationToken).ConfigureAwait(false)
                with { RequestId = RequestIdOf(response) };

            Record(activity, result);
            return result;
        }
    }

    /// <inheritdoc />
    public async Task<T> SystemOneAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        JsonContent state,
        IReadOnlyDictionary<string, Question> questions,
        JevRequestOptions? options = null,
        CancellationToken cancellationToken = default)
        where T : SystemOneResponse, new() =>
        AnswerBinder.Bind<T>(await SystemOneAsync(state, questions, options, cancellationToken).ConfigureAwait(false));

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModelCard>> ListModelsAsync(
        JevRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity("list_models", model: null);
        var response = await SendAsync(
            HttpMethod.Get, ModelsPath, body: null, options, activity, "list_models", cancellationToken).ConfigureAwait(false);

        using (response)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _log.Body("<-", payload);

            try
            {
                // A bare array is what the docs show; a wrapped one is cheap to tolerate.
                return payload.AsSpan().TrimStart() is ['[', ..]
                    ? JsonSerializer.Deserialize(payload, JevJsonContext.Default.ListModelCard) ?? []
                    : JsonSerializer.Deserialize(payload, JevJsonContext.Default.ModelList) is { } wrapper
                        ? wrapper.Data ?? wrapper.Models ?? []
                        : [];
            }
            catch (JsonException ex)
            {
                throw new JevResponseValidationException("The models response is not valid Jev JSON.", ex.Path, ex);
            }
        }
    }

    /// <summary>Builds the request body without reflecting over anything, which keeps the client AOT-safe.</summary>
    private static byte[] WriteRequest(
        JsonContent state,
        IReadOnlyDictionary<string, Question> questions,
        string model,
        IReadOnlyDictionary<string, System.Text.Json.Nodes.JsonNode?>? extraBody)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            // Extra fields go first, so the three fields this SDK owns always win.
            if (extraBody is not null)
            {
                foreach (var (name, value) in extraBody)
                {
                    if (name is "state" or "model" or "questions")
                        throw new ArgumentException($"ExtraBody cannot overwrite '{name}'.", nameof(extraBody));
                    writer.WritePropertyName(name);
                    if (value is null) writer.WriteNullValue();
                    else value.WriteTo(writer);
                }
            }

            if (state.HasValue) state.WriteTo(writer, "state");
            else writer.WriteNull("state");

            writer.WriteString("model", model);

            writer.WritePropertyName("questions");
            writer.WriteStartObject();
            foreach (var (name, question) in questions)
            {
                writer.WritePropertyName(name);
                question.Write(writer);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    /// <summary>One logical call: attempts, backoff, the retry budget, and the telemetry around them.</summary>
    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        byte[]? body,
        JevRequestOptions? options,
        Activity? activity,
        string operation,
        CancellationToken cancellationToken)
    {
        ValidateHeaders(options?.ExtraHeaders, nameof(JevRequestOptions.ExtraHeaders));

        var retry = options?.Retry ?? _options.Retry;
        retry.Validate();
        var timeout = options?.Timeout ?? _options.Timeout;
        var time = _options.TimeProvider;
        var started = time.GetTimestamp();
        var endpoint = $"{method.Method} {new Uri(_options.BaseAddress, path)}";
        var random = Random.Shared;

        for (var attempt = 0; ; attempt++)
        {
            JevException failure;
            TimeSpan? retryAfter = null;

            using var request = BuildRequest(method, path, body, options);
            _log.Attempt(method, request.RequestUri!, attempt);
            if (_log.IsDebug)
            {
                _log.Headers("->", request.Headers);
                if (body is not null) _log.Body("->", System.Text.Encoding.UTF8.GetString(body));
            }

            // The per-attempt clock comes from TimeProvider so tests can drive it; the caller's token is
            // linked in separately, which is what keeps cancellation distinguishable from a timeout.
            using var timeoutCts = timeout == Timeout.InfiniteTimeSpan ? null : new CancellationTokenSource(timeout, time);
            using var attemptCts = timeoutCts is null
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                var response = await _http
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, attemptCts.Token)
                    .ConfigureAwait(false);

                _log.Headers("<-", response.Headers);
                _log.Completed((int)response.StatusCode, RequestIdOf(response), time.GetElapsedTime(started).TotalSeconds);

                if (response.IsSuccessStatusCode)
                {
                    Complete(activity, operation, (int)response.StatusCode, attempt, started, error: null);
                    return response;
                }

                using (response)
                {
                    retryAfter = RetryPolicy.RetryAfterOf(response);
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    failure = JevApiException.From(response.StatusCode, errorBody, RequestIdOf(response), endpoint, retryAfter);
                }
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                failure = new JevTimeoutException(timeout, ex);
            }
            catch (OperationCanceledException)
            {
                Complete(activity, operation, status: null, attempt, started, error: "cancelled");
                throw;
            }
            catch (HttpRequestException ex)
            {
                failure = new JevConnectionException(ex);
            }

            var elapsed = time.GetElapsedTime(started);
            var delay = retry.Delay(attempt, retryAfter, random);
            var affordable = retry.Budget is not { } budget || elapsed + delay < budget;

            if (attempt >= retry.MaxRetries || !retry.ShouldRetry(failure) || !affordable)
            {
                _log.Failed(failure);
                Complete(activity, operation, (failure as JevApiException)?.Status is { } s ? (int)s : null,
                    attempt, started, error: ErrorTypeOf(failure));
                throw failure;
            }

            _log.Retrying(failure, delay);
            JevTelemetry.Retries.Add(1, new KeyValuePair<string, object?>("jev_net.operation", operation));
            await Task.Delay(delay, time, cancellationToken).ConfigureAwait(false);
        }
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, byte[]? body, JevRequestOptions? options)
    {
        var request = new HttpRequestMessage(method, new Uri(_options.BaseAddress, path));

        if (body is not null)
            request.Content = new ByteArrayContent(body) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("User-Agent", JevDefaults.UserAgent);
        request.Headers.TryAddWithoutValidation("X-TypeSafe-SDK", JevDefaults.UserAgent);
        request.Headers.TryAddWithoutValidation("X-TypeSafe-Runtime", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);

        foreach (var (name, value) in _options.DefaultHeaders ?? Empty)
            request.Headers.TryAddWithoutValidation(name, value);
        foreach (var (name, value) in options?.ExtraHeaders ?? Empty)
            request.Headers.TryAddWithoutValidation(name, value);

        return request;
    }

    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();

    /// <summary>The headers this client owns cannot be replaced from outside.</summary>
    private static void ValidateHeaders(IReadOnlyDictionary<string, string>? headers, string parameter)
    {
        if (headers is null) return;
        foreach (var name in headers.Keys)
            if (JevDefaults.ProtectedHeaders.Contains(name))
                throw new JevException($"Header '{name}' is set by the client and cannot be overridden through {parameter}.");
    }

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false)
                ?? throw new JevResponseValidationException("The response body was empty.");
        }
        catch (JsonException ex)
        {
            throw new JevResponseValidationException("The response body is not valid Jev JSON.", ex.Path, ex);
        }
    }

    private static string? RequestIdOf(HttpResponseMessage response) =>
        response.Headers.TryGetValues("x-typesafe-request-id", out var values) ? values.FirstOrDefault() : null;

    private Activity? StartActivity(string operation, string? model)
    {
        var activity = JevTelemetry.Source.StartActivity($"jev {operation}", ActivityKind.Client);
        if (activity is null) return null;

        activity.SetTag("jev_net.operation", operation);
        activity.SetTag("server.address", _options.BaseAddress.Host);
        activity.SetTag("url.full", new UriBuilder(_options.BaseAddress) { UserName = "", Password = "", Query = "" }.Uri.ToString());
        if (model is not null) activity.SetTag("jev_net.request.model", model);
        return activity;
    }

    private void Complete(Activity? activity, string operation, int? status, int attempt, long started, string? error)
    {
        var elapsed = _options.TimeProvider.GetElapsedTime(started);

        var tags = new TagList
        {
            { "jev_net.operation", operation },
            { "http.response.status_code", status },
        };
        if (error is not null) tags.Add("error.type", error);
        JevTelemetry.Duration.Record(elapsed.TotalSeconds, tags);

        if (activity is null) return;
        activity.SetTag("http.response.status_code", status);
        activity.SetTag("http.request.resend_count", attempt);
        if (error is not null)
        {
            // Fixed text only: the server's message could echo the state that was sent.
            activity.SetStatus(ActivityStatusCode.Error, status is { } code ? $"HTTP {code}" : error);
            activity.SetTag("error.type", error);
        }
    }

    private static void Record(Activity? activity, SystemOneResponse result)
    {
        if (result.Usage?.InputTokens is { } input)
            JevTelemetry.Tokens.Add(input, new KeyValuePair<string, object?>("jev_net.token.type", "input"),
                new KeyValuePair<string, object?>("jev_net.response.model", result.Model));
        if (result.Usage?.OutputTokens is { } output)
            JevTelemetry.Tokens.Add(output, new KeyValuePair<string, object?>("jev_net.token.type", "output"),
                new KeyValuePair<string, object?>("jev_net.response.model", result.Model));

        activity?.SetTag("jev_net.response.model", result.Model);
        activity?.SetTag("jev_net.usage.input_tokens", result.Usage?.InputTokens);
        activity?.SetTag("jev_net.request_id", result.RequestId);
    }

    private static string ErrorTypeOf(JevException failure) => failure switch
    {
        JevTimeoutException => "timeout",
        JevConnectionException => "connection",
        JevApiException api => $"http_{(int)api.Status}",
        _ => failure.GetType().Name,
    };

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposeHttp) _http.Dispose();
    }
}
