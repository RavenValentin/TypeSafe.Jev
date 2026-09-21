using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Jev.Net;

// Exercises every path that could break once the trimmer and the AOT compiler have had their way:
// request encoding, a retry, source-generated JSON on the way back, the reflection-backed typed response,
// and error mapping. It runs offline against a stub handler, so CI needs no key and no network.
//
// Any failure here exits nonzero and fails the build.

var checks = 0;
var failures = new List<string>();

void Check(string what, bool ok)
{
    checks++;
    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {what}");
    if (!ok) failures.Add(what);
}

const string Body = """
    {
      "model": "jev-1.13.0",
      "answers": {
        "urgent":   { "type": "noul", "noul": 0.98 },
        "category": { "type": "choice", "choice": "technical_issue", "confidence": 0.9,
                      "probabilities": { "billing": 0.1, "technical_issue": 0.9 } },
        "tone":     { "type": "score", "score": 1.7, "confidence": 0.8,
                      "legend": { "0": "calm", "1": "angry" },
                      "probabilities": { "0": 0.3, "1": 0.7 } }
      },
      "usage": { "input_tokens": 12, "output_tokens": 3 }
    }
    """;

var handler = new ScriptedHandler(
[
    new(HttpStatusCode.TooManyRequests, "{}"),   // forces one retry through the real policy
    new(HttpStatusCode.OK, Body),
]);

using var client = new JevClient(new HttpClient(handler), new JevClientOptions
{
    ApiKey = "smoke",
    Retry = RetryPolicy.Default with { BackoffInitial = TimeSpan.FromMilliseconds(1), BackoffMax = TimeSpan.FromMilliseconds(1) },
});

var questions = new Dictionary<string, Question>
{
    ["urgent"] = Question.Noul("Is it urgent?"),
    ["category"] = Question.Choice<Category>("What is this about?"),
    ["tone"] = Question.Score("How angry?", "calm", "angry"),
    ["raw"] = Question.Raw(new JsonObject { ["type"] = "noul", ["instructions"] = "Raw question" }),
};

// A typed response: this is the one place the library reflects, so it is the one most at risk from trimming.
var ticket = await client.SystemOneAsync<Ticket>(new JsonObject { ["document"] = "the webhook is down" }, questions);

Check("retried once and then succeeded", handler.Calls == 2);
Check("noul decoded", Math.Abs(ticket.Urgent.Noul - 0.98) < 1e-9);
Check("choice mapped back to the enum", ticket.Category.As<Category>() == Category.TechnicalIssue);
Check("score legend keyed by int", ticket.Tone.Legend[1]?.GetValue<string>() == "angry");
Check("usage decoded", ticket.Usage?.InputTokens == 12);
Check("optional answer left null", ticket.Spam is null);

var sent = JsonNode.Parse(handler.LastBody!)!;
Check("state encoded", (string?)sent["state"]!["document"] == "the webhook is down");
Check("enum option names are snake_case", sent["questions"]!["category"]!["criteria"]!["technical_issue"] is not null);
Check("[Description] became the rubric",
    (string?)sent["questions"]!["category"]!["criteria"]!["technical_issue"] == "Bugs and outages");
Check("raw question passed through", (string?)sent["questions"]!["raw"]!["instructions"] == "Raw question");

// Error mapping, all the way down to the concrete exception type.
using var failing = new JevClient(
    new HttpClient(new ScriptedHandler([new(HttpStatusCode.UnprocessableEntity, """{"message":"bad rubric"}""")])),
    new JevClientOptions { ApiKey = "smoke", Retry = RetryPolicy.None });

try
{
    await failing.SystemOneAsync("s", questions);
    Check("422 threw", false);
}
catch (JevUnprocessableEntityException error)
{
    Check("422 mapped to its exception", true);
    Check("error message carries the server's explanation", error.Message.Contains("bad rubric"));
}

Console.WriteLine($"\n{checks - failures.Count}/{checks} checks passed");
if (failures.Count == 0) return 0;

Console.Error.WriteLine("failed: " + string.Join(", ", failures));
return 1;

internal enum Category
{
    [Description("Charges and refunds")] Billing,
    [Description("Bugs and outages")] TechnicalIssue,
}

internal sealed record Ticket : SystemOneResponse
{
    public NoulAnswer Urgent { get; set; } = null!;
    public ChoiceAnswer Category { get; set; } = null!;
    public ScoreAnswer Tone { get; set; } = null!;
    [OptionalAnswer] public NoulAnswer? Spam { get; set; }
}

internal sealed record Scripted(HttpStatusCode Status, string Body);

internal sealed class ScriptedHandler(Scripted[] replies) : HttpMessageHandler
{
    public int Calls { get; private set; }

    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is not null) LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
        var reply = replies[Math.Min(Calls, replies.Length - 1)];
        Calls++;
        return new HttpResponseMessage(reply.Status)
        {
            Content = new StringContent(reply.Body, Encoding.UTF8, "application/json"),
        };
    }
}
