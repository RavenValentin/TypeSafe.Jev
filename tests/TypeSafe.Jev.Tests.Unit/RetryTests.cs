using System.Net;
using static TypeSafe.Jev.Tests.Unit.Fixtures;

namespace TypeSafe.Jev.Tests.Unit;

public class RetryTests
{
    [Fact]
    public void ShouldRetry_follows_the_documented_rules()
    {
        var policy = RetryPolicy.Default;

        Assert.True(policy.ShouldRetry(new JevRateLimitException(HttpStatusCode.TooManyRequests, null, null)));
        Assert.True(policy.ShouldRetry(new JevInternalServerException((HttpStatusCode)529, null, null)));
        Assert.True(policy.ShouldRetry(new JevApiException(HttpStatusCode.RequestTimeout, null, null)));
        Assert.True(policy.ShouldRetry(new JevTimeoutException(TimeSpan.FromSeconds(1))));
        Assert.True(policy.ShouldRetry(new JevConnectionException(new HttpRequestException())));

        Assert.False(policy.ShouldRetry(new JevAuthenticationException(HttpStatusCode.Unauthorized, null, null)));
        Assert.False(policy.ShouldRetry(new JevUnprocessableEntityException(HttpStatusCode.UnprocessableEntity, null, null)));
        Assert.False(policy.ShouldRetry(new JevBadRequestException(HttpStatusCode.BadRequest, null, null)));
    }

    [Fact]
    public void A_predicate_can_only_narrow_the_rules()
    {
        var policy = RetryPolicy.Default with { Predicate = e => e is not JevRateLimitException };

        Assert.False(policy.ShouldRetry(new JevRateLimitException(HttpStatusCode.TooManyRequests, null, null)));
        Assert.True(policy.ShouldRetry(new JevInternalServerException(HttpStatusCode.InternalServerError, null, null)));
        // A 401 is still not retried, whatever the predicate says.
        Assert.False((RetryPolicy.Default with { Predicate = _ => true })
            .ShouldRetry(new JevAuthenticationException(HttpStatusCode.Unauthorized, null, null)));
    }

    [Fact]
    public void Backoff_doubles_up_to_the_ceiling_and_keeps_jitter_in_range()
    {
        var policy = RetryPolicy.Default;
        var random = new Random(1);

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var delay = policy.Delay(attempt, retryAfter: null, random);
            var nominal = Math.Min(500 * Math.Pow(2, attempt), 5000);
            Assert.InRange(delay.TotalMilliseconds, nominal * 0.75, nominal);
        }
    }

    [Fact]
    public void Retry_after_wins_until_it_exceeds_the_cap()
    {
        var policy = RetryPolicy.Default;
        var random = new Random(1);

        Assert.Equal(TimeSpan.FromSeconds(3), policy.Delay(0, TimeSpan.FromSeconds(3), random));

        // Longer than MaxRetryAfter: fall back to backoff rather than sleeping for minutes.
        var tooLong = policy.Delay(0, TimeSpan.FromMinutes(5), random);
        Assert.InRange(tooLong.TotalMilliseconds, 375, 500);

        var ignored = (policy with { RespectRetryAfter = false }).Delay(0, TimeSpan.FromSeconds(3), random);
        Assert.InRange(ignored.TotalMilliseconds, 375, 500);
    }

    [Fact]
    public void Nonsense_policies_are_rejected()
    {
        Assert.Throws<JevException>(() => (RetryPolicy.Default with { MaxRetries = -1 }).Validate());
        Assert.Throws<JevException>(() => (RetryPolicy.Default with { BackoffJitter = 2 }).Validate());
        Assert.Throws<JevException>(() => (RetryPolicy.Default with { Budget = TimeSpan.Zero }).Validate());
        Assert.Throws<JevException>(() =>
            (RetryPolicy.Default with { BackoffInitial = TimeSpan.FromSeconds(10) }).Validate());
    }

    [Fact]
    public async Task Retries_a_429_then_a_529_then_succeeds()
    {
        var handler = new StubHandler(
            StubHandler.Json(HttpStatusCode.TooManyRequests, "", ("retry-after-ms", "1")),
            StubHandler.Json((HttpStatusCode)529, ""),
            Ok());
        using var client = Client(handler);

        var result = await client.SystemOneAsync("s", Questions);

        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal("jev-1.13.0", result.Model);
    }

    [Fact]
    public async Task Gives_up_after_MaxRetries_and_surfaces_the_last_failure()
    {
        var handler = new StubHandler(StubHandler.Json(HttpStatusCode.TooManyRequests, "", ("Retry-After", "1")));
        using var client = Client(handler);

        var error = await Assert.ThrowsAsync<JevRateLimitException>(() => client.SystemOneAsync("s", Questions));

        Assert.Equal(TimeSpan.FromSeconds(1), error.RetryAfter);
        Assert.Equal(3, handler.Requests.Count);      // the first attempt plus two retries
    }

    [Fact]
    public async Task Does_not_retry_a_422()
    {
        var handler = new StubHandler(StubHandler.Json(HttpStatusCode.UnprocessableEntity, "{}"));
        using var client = Client(handler);

        await Assert.ThrowsAsync<JevUnprocessableEntityException>(() => client.SystemOneAsync("s", Questions));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task A_spent_budget_stops_the_next_retry()
    {
        var handler = new StubHandler(StubHandler.Json(HttpStatusCode.ServiceUnavailable, ""));
        using var client = Client(handler, new JevClientOptions
        {
            ApiKey = "k",
            // The first backoff alone is far longer than the budget, so no retry is affordable.
            Retry = RetryPolicy.Default with
            {
                MaxRetries = 5,
                BackoffInitial = TimeSpan.FromSeconds(10),
                BackoffMax = TimeSpan.FromSeconds(10),
                Budget = TimeSpan.FromMilliseconds(1),
            },
        });

        await Assert.ThrowsAsync<JevInternalServerException>(() => client.SystemOneAsync("s", Questions));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task RetryPolicy_None_never_retries()
    {
        var handler = new StubHandler(StubHandler.Json(HttpStatusCode.ServiceUnavailable, ""));
        using var client = Client(handler, new JevClientOptions { ApiKey = "k", Retry = RetryPolicy.None });

        await Assert.ThrowsAsync<JevInternalServerException>(() => client.SystemOneAsync("s", Questions));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task A_per_call_policy_overrides_the_client_one()
    {
        var handler = new StubHandler(StubHandler.Json(HttpStatusCode.ServiceUnavailable, ""));
        using var client = Client(handler);

        await Assert.ThrowsAsync<JevInternalServerException>(
            () => client.SystemOneAsync("s", Questions, new JevRequestOptions { Retry = RetryPolicy.None }));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Connection_failures_are_retried_and_then_wrapped()
    {
        var handler = new StubHandler(Ok()) { Throw = _ => new HttpRequestException("no route to host") };
        using var client = Client(handler);

        var error = await Assert.ThrowsAsync<JevConnectionException>(() => client.SystemOneAsync("s", Questions));

        Assert.Equal(3, handler.Requests.Count);
        Assert.IsType<HttpRequestException>(error.InnerException);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_a_timeout_and_is_never_retried()
    {
        var handler = new StubHandler(Ok());
        using var client = Client(handler);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SystemOneAsync("s", Questions, cancellationToken: cts.Token));
    }
}
