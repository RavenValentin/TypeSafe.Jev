using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jev.Net;

/// <summary>Default <see cref="IJevClient"/> over <see cref="HttpClient"/> with retries matching the official SDKs.</summary>
public sealed class JevClient : IJevClient, IDisposable
{
    /// <summary>Serializer settings matching the Jev wire format.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly JevClientOptions _options;
    private readonly bool _ownsHttp;

    /// <summary>Uses a caller-supplied <see cref="HttpClient"/> (e.g. from <c>IHttpClientFactory</c>).</summary>
    public JevClient(HttpClient httpClient, JevClientOptions options)
    {
        options.Validate();
        _http = httpClient;
        _options = options;
        _http.Timeout = System.Threading.Timeout.InfiniteTimeSpan; // per-attempt timeout is enforced in SendAsync
    }

    /// <summary>Creates a client that owns its own <see cref="HttpClient"/>.</summary>
    public JevClient(JevClientOptions? options = null) : this(new HttpClient(), options ?? new JevClientOptions()) => _ownsHttp = true;

    /// <summary>Creates a client for <paramref name="apiKey"/> with default options.</summary>
    public JevClient(string apiKey) : this(new JevClientOptions { ApiKey = apiKey }) { }

    /// <inheritdoc />
    public async Task<SystemOneResponse> SystemOneAsync(
        object? state, IReadOnlyDictionary<string, Question> questions, string? model = null, CancellationToken cancellationToken = default)
    {
        if (questions.Count == 0) throw new ArgumentException("At least one question is required.", nameof(questions));

        var payload = new { state, model = model ?? _options.DefaultModel, questions };
        var body = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);

        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, "v1/systemone")
            {
                Content = new ByteArrayContent(body) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } },
            },
            cancellationToken).ConfigureAwait(false);

        var result = await ReadAsync<SystemOneResponse>(response, cancellationToken).ConfigureAwait(false);
        return result with { RequestId = RequestId(response) };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModelCard>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, "v1/models"), cancellationToken).ConfigureAwait(false);
        return await ReadAsync<List<ModelCard>>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends with a per-attempt timeout and retries; returns a success response or throws a <see cref="JevException"/>.</summary>
    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> request, CancellationToken ct)
    {
        // ponytail: hand-rolled retry loop mirroring the official SDK policy; swap for Microsoft.Extensions.Http.Resilience if policies must be pluggable.
        for (var attempt = 0; ; attempt++)
        {
            JevException failure;
            TimeSpan? retryAfter = null;

            using var req = request();
            req.RequestUri = new Uri(_options.BaseAddress, req.RequestUri!);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            attemptCts.CancelAfter(_options.Timeout);
            try
            {
                var response = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, attemptCts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode) return response;

                using (response)
                {
                    retryAfter = RetryAfter(response);
                    var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    failure = JevApiException.From(response.StatusCode, responseBody, RequestId(response), retryAfter);
                }
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                failure = new JevTimeoutException(_options.Timeout, ex);
            }
            catch (HttpRequestException ex)
            {
                failure = new JevConnectionException(ex);
            }

            if (attempt >= _options.MaxRetries || !IsRetryable(failure)) throw failure;

            var delay = retryAfter is { } ra && ra <= _options.MaxRetryAfter ? ra : Backoff(attempt);
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
    }

    private static bool IsRetryable(JevException e) => e switch
    {
        JevApiException api => (int)api.Status is 408 or 429 or >= 500,
        JevTimeoutException or JevConnectionException => true,
        _ => false,
    };

    private static TimeSpan Backoff(int attempt)
    {
        var ms = Math.Min(500 * (1 << attempt), 5000);
        return TimeSpan.FromMilliseconds(ms * (1 - 0.25 * Random.Shared.NextDouble()));
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("retry-after-ms", out var ms) && double.TryParse(ms.FirstOrDefault(), out var v))
            return TimeSpan.FromMilliseconds(v);
        var ra = response.Headers.RetryAfter;
        return ra?.Delta ?? (ra?.Date is { } d ? d - DateTimeOffset.UtcNow : null);
    }

    private static string? RequestId(HttpResponseMessage response) =>
        response.Headers.TryGetValues("x-typesafe-request-id", out var v) ? v.FirstOrDefault() : null;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false)
                ?? throw new JevResponseValidationException("Empty response body.");
        }
        catch (JsonException ex)
        {
            throw new JevResponseValidationException("Response body is not valid Jev JSON.", ex.Path, ex);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}
