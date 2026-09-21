using System.ComponentModel;
using System.Net;

namespace TypeSafe.Jev.Tests.Unit;

internal static class Fixtures
{
    public enum Category
    {
        [Description("Charges, refunds, invoices")] Billing,
        [Description("Bugs and outages")] TechnicalIssue,
        Other,
    }

    public const string Answers = """
        {
          "model": "jev-1.13.0",
          "answers": {
            "urgent":   { "noul": 0.98, "type": "noul" },
            "category": { "type": "choice", "choice": "technical_issue", "confidence": 0.9,
                          "probabilities": { "billing": 0.05, "technical_issue": 0.9, "other": 0.05 } },
            "tone":     { "type": "score", "score": 1.7, "confidence": 0.8,
                          "legend": { "0": "calm", "1": "tense", "2": "angry" },
                          "probabilities": { "0": 0.1, "1": 0.1, "2": 0.8 } }
          },
          "usage": { "input_tokens": 392, "output_tokens": 65 }
        }
        """;

    public static Dictionary<string, Question> Questions => new()
    {
        ["urgent"] = Question.Noul("Is it urgent?"),
        ["category"] = Question.Choice<Category>("What is this about?"),
        ["tone"] = Question.Score("How angry?", "calm", "tense", "angry"),
    };

    /// <summary>
    /// A client whose retries are instant, so tests never wait on real backoff. A policy the caller
    /// supplied is left exactly as given — tests about timing need their own numbers to mean something.
    /// </summary>
    public static JevClient Client(StubHandler handler, JevClientOptions? options = null)
    {
        options ??= new JevClientOptions();
        options.ApiKey ??= "test-key";
        if (ReferenceEquals(options.Retry, RetryPolicy.Default))
        {
            options.Retry = options.Retry with
            {
                BackoffInitial = TimeSpan.FromMilliseconds(1),
                BackoffMax = TimeSpan.FromMilliseconds(1),
                MaxRetryAfter = TimeSpan.FromMilliseconds(5),
            };
        }

        return new JevClient(new HttpClient(handler), options);
    }

    public static Reply Ok(params (string, string)[] headers) =>
        StubHandler.Json(HttpStatusCode.OK, Answers, headers);
}
