using System.Diagnostics;
using System.Net;
using static Jev.Net.Tests.Unit.Fixtures;

namespace Jev.Net.Tests.Unit;

public class ClientTests
{
    [Fact]
    public void A_missing_api_key_fails_at_construction()
    {
        var error = Assert.Throws<JevException>(() => new JevClient(new HttpClient(), new JevClientOptions { ApiKey = " " }));
        Assert.Contains("TYPESAFE_API_KEY", error.Message);
    }

    [Fact]
    public void A_nonsense_timeout_fails_at_construction()
    {
        Assert.Throws<JevException>(() =>
            new JevClient(new HttpClient(), new JevClientOptions { ApiKey = "k", Timeout = TimeSpan.Zero }));
    }

    [Theory]
    [InlineData(400, typeof(JevBadRequestException))]
    [InlineData(401, typeof(JevAuthenticationException))]
    [InlineData(403, typeof(JevPermissionDeniedException))]
    [InlineData(404, typeof(JevNotFoundException))]
    [InlineData(422, typeof(JevUnprocessableEntityException))]
    [InlineData(429, typeof(JevRateLimitException))]
    [InlineData(500, typeof(JevInternalServerException))]
    [InlineData(529, typeof(JevInternalServerException))]
    public async Task Statuses_map_to_their_exception(int status, Type expected)
    {
        var handler = new StubHandler(StubHandler.Json((HttpStatusCode)status, "{}"));
        using var client = Client(handler, new JevClientOptions { ApiKey = "k", Retry = RetryPolicy.None });

        var error = await Record.ExceptionAsync(() => client.SystemOneAsync("s", Questions));

        Assert.IsType(expected, error);
    }

    [Fact]
    public async Task An_api_error_carries_status_body_request_id_and_endpoint()
    {
        var handler = new StubHandler(StubHandler.Json(
            HttpStatusCode.TooManyRequests, """{"message":"slow down"}""", ("x-typesafe-request-id", "req-9")));
        using var client = Client(handler, new JevClientOptions { ApiKey = "k", Retry = RetryPolicy.None });

        var error = await Assert.ThrowsAsync<JevRateLimitException>(() => client.SystemOneAsync("s", Questions));

        Assert.Equal(HttpStatusCode.TooManyRequests, error.Status);
        Assert.Contains("slow down", error.Body);
        Assert.Equal("req-9", error.RequestId);
        Assert.Equal("POST https://api.typesafe.ai/v1/systemone", error.Endpoint);
        Assert.Equal("POST https://api.typesafe.ai/v1/systemone: 429 slow down (request_id=req-9)", error.Message);
    }

    [Fact]
    public async Task An_error_body_that_is_not_json_still_produces_a_usable_message()
    {
        var handler = new StubHandler(StubHandler.Json(HttpStatusCode.BadGateway, "<html>nginx</html>"));
        using var client = Client(handler, new JevClientOptions { ApiKey = "k", Retry = RetryPolicy.None });

        var error = await Assert.ThrowsAsync<JevInternalServerException>(() => client.SystemOneAsync("s", Questions));

        Assert.Contains("502", error.Message);
        Assert.Contains("<html>nginx</html>", error.Body);
    }

    [Fact]
    public async Task A_supplied_HttpClient_is_left_alone_unless_asked()
    {
        var http = new HttpClient(new StubHandler(Ok()));
        var client = new JevClient(http, new JevClientOptions { ApiKey = "k" });

        client.Dispose();

        // Still usable: the client did not dispose what it does not own.
        using var response = await http.GetAsync("https://api.typesafe.ai/v1/models");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Base_address_and_default_model_are_honoured()
    {
        var handler = new StubHandler(Ok());
        using var client = Client(handler, new JevClientOptions
        {
            ApiKey = "k",
            BaseAddress = new Uri("https://proxy.internal/jev/"),
            DefaultModel = "jev-1.13.0",
        });

        await client.SystemOneAsync("s", Questions);

        Assert.Equal("https://proxy.internal/jev/v1/systemone", handler.Requests.Single().Uri);
        Assert.Contains("\"model\":\"jev-1.13.0\"", handler.Requests.Single().Body);
    }

    [Fact]
    public async Task Logging_records_attempts_and_redacts_credentials()
    {
        var logs = new RecordingLoggerFactory();
        var handler = new StubHandler(Ok(("x-typesafe-request-id", "req-1")));
        using var client = Client(handler, new JevClientOptions { ApiKey = "super-secret", LoggerFactory = logs });

        await client.SystemOneAsync("s", Questions);

        Assert.Contains(logs.Entries, e => e.Contains("POST https://api.typesafe.ai/v1/systemone"));
        Assert.Contains(logs.Entries, e => e.Contains("-> 200"));
        Assert.Contains(logs.Entries, e => e.Contains("Authorization") && e.Contains("<redacted>"));
        Assert.DoesNotContain(logs.Entries, e => e.Contains("super-secret"));
    }

    [Fact]
    public async Task Without_a_logger_factory_nothing_is_logged()
    {
        var handler = new StubHandler(Ok());
        using var client = Client(handler);

        await client.SystemOneAsync("s", Questions);   // must not throw on the null logger path
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task A_span_is_produced_with_operation_model_and_status()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == JevTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Add,
        };
        ActivitySource.AddActivityListener(listener);

        // The listener is process-wide, so other tests running in parallel land in the same list.
        // A model name unique to this test is what makes the assertion about *our* call.
        const string probe = "probe-success";
        using var client = Client(new StubHandler(Ok()));
        await client.SystemOneAsync("s", Questions, new JevRequestOptions { Model = probe });

        var activity = Assert.Single(activities, a => (string?)a.GetTagItem("jev_net.request.model") == probe);
        Assert.Equal("jev system_one", activity.OperationName);
        Assert.Equal("system_one", activity.GetTagItem("jev_net.operation"));
        Assert.Equal(200, activity.GetTagItem("http.response.status_code"));
        Assert.Equal("jev-1.13.0", activity.GetTagItem("jev_net.response.model"));
        Assert.Equal(0, activity.GetTagItem("http.request.resend_count"));
        Assert.Equal("api.typesafe.ai", activity.GetTagItem("server.address"));
    }

    [Fact]
    public async Task A_failed_span_carries_a_fixed_description_not_the_servers_words()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == JevTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Add,
        };
        ActivitySource.AddActivityListener(listener);

        const string probe = "probe-failure";
        var handler = new StubHandler(StubHandler.Json(HttpStatusCode.Unauthorized, """{"message":"key sk-abc is invalid"}"""));
        using var client = Client(handler, new JevClientOptions { ApiKey = "k", Retry = RetryPolicy.None });

        await Assert.ThrowsAsync<JevAuthenticationException>(
            () => client.SystemOneAsync("s", Questions, new JevRequestOptions { Model = probe }));

        var activity = Assert.Single(activities, a => (string?)a.GetTagItem("jev_net.request.model") == probe);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("HTTP 401", activity.StatusDescription);
        Assert.Equal("http_401", activity.GetTagItem("error.type"));
        Assert.DoesNotContain("sk-abc", activity.StatusDescription);
    }
}
