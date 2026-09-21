# Jev.Net

Unofficial .NET client for [Jev](https://docs.typesafe.ai), TypeSafe AI's System One decision model.
Ask typed **noul** (yes/no), **choice** and **score** questions about any state and get back probabilities and confidence — no text parsing.

```bash
dotnet add package Jev.Net
```

## Quickstart

```csharp
using System.ComponentModel;
using Jev.Net;

enum Category
{
    [Description("Charges, refunds, invoices")] Billing,
    [Description("Bugs, integrations, outages")] Technical,
    Other,
}

var client = new JevClient(); // reads TYPESAFE_API_KEY

var res = await client.SystemOneAsync(
    state: new { document = "I was charged twice. Please fix this ASAP." },
    questions: new Dictionary<string, Question>
    {
        ["urgent"]   = Question.Noul("Does this message express urgency?"),
        ["category"] = Question.Choice<Category>("What is this ticket about?"),
        ["anger"]    = Question.Score("How angry is the customer?", "calm", "tense", "furious"),
    });

double urgency  = res.Noul("urgent").Noul;                 // 0..1
Category cat    = res.Choice("category").As<Category>();   // typed enum
double conf     = res.Choice("category").Confidence;
double anger    = res.Score("anger").Score;                // 0..2, may be fractional
```

Enum members become snake_case option names; `[Description]` becomes the option rubric.

## Configuration

```csharp
var client = new JevClient(new JevClientOptions
{
    ApiKey = "...",                       // default: TYPESAFE_API_KEY
    BaseAddress = new("https://api.typesafe.ai"),
    DefaultModel = "jev-latest",
    Timeout = TimeSpan.FromSeconds(10),   // per attempt
    MaxRetries = 2,                       // 408/429/5xx, connection and timeout errors
});
```

Or bring your own `HttpClient`: `new JevClient(httpClient, options)`.

## Errors

All failures derive from `JevException`:

| Exception | When |
|---|---|
| `JevAuthenticationException` | 401 |
| `JevPermissionDeniedException` | 403 |
| `JevUnprocessableEntityException` | 422 — request validation |
| `JevRateLimitException` | 429 — exposes `RetryAfter` |
| `JevInternalServerException` | 5xx incl. 529 overloaded |
| `JevTimeoutException` / `JevConnectionException` | no HTTP response |
| `JevResponseValidationException` | 2xx with unexpected shape |

Every `JevApiException` carries `Status`, raw `Body` and `RequestId`.

## License

MIT
