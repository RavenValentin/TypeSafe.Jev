using System.ComponentModel;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Jev.Net.Tests.Unit;

public class JevClientTests
{
    private enum Category
    {
        [Description("Charges, refunds, invoices")] Billing,
        [Description("Bugs and outages")] TechnicalIssue,
        Other,
    }

    private const string Ok = """
        {
          "model": "jev-1.13.0",
          "answers": {
            "urgent":   { "noul": 0.98, "type": "noul" },
            "category": { "type": "choice", "choice": "technical_issue", "confidence": 0.9, "probabilities": { "billing": 0.05, "technical_issue": 0.9, "other": 0.05 } },
            "tone":     { "type": "score", "score": 1.7, "confidence": 0.8, "legend": { "0": "calm", "1": "tense", "2": "angry" }, "probabilities": { "0": 0.1, "1": 0.1, "2": 0.8 } }
          },
          "usage": { "input_tokens": 392, "output_tokens": 65 }
        }
        """;

    private static (JevClient client, FakeHandler handler) Make(params HttpResponseMessage[] responses)
    {
        var handler = new FakeHandler(responses);
        var client = new JevClient(new HttpClient(handler), new JevClientOptions { ApiKey = "k", MaxRetries = 2 });
        return (client, handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body, params (string, string)[] headers)
    {
        var r = new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        foreach (var (k, v) in headers) r.Headers.TryAddWithoutValidation(k, v);
        return r;
    }

    private static readonly Dictionary<string, Question> Questions = new()
    {
        ["urgent"] = Question.Noul("Is it urgent?"),
        ["category"] = Question.Choice<Category>("What is this about?"),
        ["tone"] = Question.Score("How angry?", "calm", "tense", "angry"),
    };

    [Fact]
    public async Task Serializes_request_in_wire_format()
    {
        var (client, handler) = Make(Json(HttpStatusCode.OK, Ok, ("x-typesafe-request-id", "req-1")));

        await client.SystemOneAsync(new { document = "hi" }, Questions);

        var req = handler.Requests.Single();
        Assert.Equal("https://api.typesafe.ai/v1/systemone", req.Uri);
        Assert.Equal("Bearer k", req.Authorization);
        var json = JsonNode.Parse(req.Body)!;
        Assert.Equal("jev-latest", (string?)json["model"]);
        Assert.Equal("hi", (string?)json["state"]!["document"]);
        Assert.Equal("noul", (string?)json["questions"]!["urgent"]!["type"]);
        Assert.Null(json["questions"]!["urgent"]!["criteria"]);
        var choice = json["questions"]!["category"]!;
        Assert.Equal("choice", (string?)choice["type"]);
        Assert.Equal("Charges, refunds, invoices", (string?)choice["criteria"]!["billing"]);
        Assert.Equal("Bugs and outages", (string?)choice["criteria"]!["technical_issue"]);
        Assert.True(choice["criteria"]!.AsObject().ContainsKey("other"));
        Assert.Null(choice["criteria"]!["other"]);
        Assert.Equal(3, json["questions"]!["tone"]!["criteria"]!.AsArray().Count);
    }

    [Fact]
    public async Task Parses_all_answer_types()
    {
        var (client, _) = Make(Json(HttpStatusCode.OK, Ok, ("x-typesafe-request-id", "req-1")));

        var res = await client.SystemOneAsync("text", Questions);

        Assert.Equal("jev-1.13.0", res.Model);
        Assert.Equal("req-1", res.RequestId);
        Assert.Equal(392, res.Usage!.InputTokens);
        Assert.Equal(0.98, res.Noul("urgent").Noul);
        Assert.Equal(Category.TechnicalIssue, res.Choice("category").As<Category>());
        Assert.Equal(0.9, res.Choice("category").Probabilities["technical_issue"]);
        var tone = res.Score("tone");
        Assert.Equal(1.7, tone.Score);
        Assert.Equal("angry", (string?)tone.Legend[2]);
        Assert.Equal(0.8, tone.Probabilities[2]);
        Assert.Throws<JevResponseValidationException>(() => res.Noul("category"));
        Assert.Throws<JevResponseValidationException>(() => res.Noul("missing"));
    }

    [Fact]
    public async Task Maps_401_to_authentication_exception_without_retry()
    {
        var (client, handler) = Make(Json(HttpStatusCode.Unauthorized, """{"error":"bad key"}""", ("x-typesafe-request-id", "r")));

        var ex = await Assert.ThrowsAsync<JevAuthenticationException>(() => client.SystemOneAsync("s", Questions));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.Status);
        Assert.Contains("bad key", ex.Body);
        Assert.Equal("r", ex.RequestId);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Retries_429_honoring_retry_after_then_succeeds()
    {
        var (client, handler) = Make(
            Json(HttpStatusCode.TooManyRequests, "", ("retry-after-ms", "10")),
            Json((HttpStatusCode)529, ""),
            Json(HttpStatusCode.OK, Ok));

        var res = await client.SystemOneAsync("s", Questions);

        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal("jev-1.13.0", res.Model);
    }

    [Fact]
    public async Task Gives_up_after_max_retries_with_rate_limit_exception()
    {
        var (client, handler) = Make(
            Json(HttpStatusCode.TooManyRequests, "", ("retry-after-ms", "1")),
            Json(HttpStatusCode.TooManyRequests, "", ("retry-after-ms", "1")),
            Json(HttpStatusCode.TooManyRequests, "", ("Retry-After", "1")));

        var ex = await Assert.ThrowsAsync<JevRateLimitException>(() => client.SystemOneAsync("s", Questions));

        Assert.Equal(TimeSpan.FromSeconds(1), ex.RetryAfter);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task Does_not_retry_422()
    {
        var (client, handler) = Make(Json(HttpStatusCode.UnprocessableEntity, "{}"));

        await Assert.ThrowsAsync<JevUnprocessableEntityException>(() => client.SystemOneAsync("s", Questions));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Unknown_answer_type_is_validation_error()
    {
        var (client, _) = Make(Json(HttpStatusCode.OK, """{"model":"m","answers":{"q":{"type":"weird"}}}"""));

        await Assert.ThrowsAsync<JevResponseValidationException>(() => client.SystemOneAsync("s", Questions));
    }

    [Fact]
    public void Missing_api_key_fails_fast()
    {
        Assert.Throws<JevException>(() => new JevClient(new HttpClient(), new JevClientOptions { ApiKey = "" }));
    }

    [Fact]
    public void Enum_wire_names_round_trip()
    {
        Assert.Equal("technical_issue", EnumNames.ToWire(Category.TechnicalIssue));
        Assert.Equal(Category.TechnicalIssue, EnumNames.FromWire<Category>("technical_issue"));
        Assert.Null(EnumNames.FromWire<Category>("nope"));
    }

    private sealed class FakeHandler(HttpResponseMessage[] responses) : HttpMessageHandler
    {
        public List<(string Uri, string? Authorization, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(), body));
            return responses[Math.Min(Requests.Count - 1, responses.Length - 1)];
        }
    }
}
