# TypeSafe.Jev

[![CI](https://github.com/RavenValentin/TypeSafe.Jev/actions/workflows/ci.yml/badge.svg)](https://github.com/RavenValentin/TypeSafe.Jev/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/TypeSafe.Jev.svg?logo=nuget)](https://www.nuget.org/packages/TypeSafe.Jev)
[![Downloads](https://img.shields.io/nuget/dt/TypeSafe.Jev.svg)](https://www.nuget.org/packages/TypeSafe.Jev)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**Ask an AI a typed question. Get an answer your `switch` statement can use.**

```csharp
Category category = res.Choice("category").As<Category>();   // a real enum, not a string to parse
```

[Jev](https://docs.typesafe.ai) is TypeSafe AI's System One model. You hand it some state and a set of small
typed questions, and it answers each one with a **value, a probability distribution and a confidence score**.
No prompt engineering, no "please respond only with JSON", no stripping code fences off the reply.

## Features

- **Your enum is the question.** Declare the options once, get that enum back, and let the compiler find the
  `switch` branch you forgot when you add a member.
- **Confidence is a first-class number.** Auto-approve above your threshold and route the rest to a human.
- **One dependency.** `Microsoft.Extensions.Logging.Abstractions`, and nothing else. CI reads the packed
  `.nuspec` and fails on a second one.
- **Trim- and Native AOT-safe.** A native binary is published and run on every build to prove it.
- **Retries that match the official SDKs.** 408/429/5xx, backoff with jitter, `Retry-After`, per-attempt
  timeouts and a budget for the whole call. No Polly.
- **Logging and OpenTelemetry built in**, through `ILogger`, `ActivitySource` and `Meter` — no extra packages.
- **Testable offline.** The examples, and your own tests, can run without an API key.

## Requirements

| | |
|---|---|
| Target frameworks | `net8.0`, `net9.0`, `net10.0` |
| Language | C# 12 or later |
| API key | `TYPESAFE_API_KEY` from [typesafe.ai](https://typesafe.ai) |

## Install

```bash
dotnet add package TypeSafe.Jev
```

```xml
<PackageReference Include="TypeSafe.Jev" Version="0.1.0" />
```

## Quickstart

```csharp
using System.ComponentModel;
using TypeSafe.Jev;

enum Category
{
    [Description("Charges, refunds, invoices, subscription changes")] Billing,
    [Description("Bugs, outages, broken integrations")]               TechnicalIssue,
    [Description("Asking for something the product cannot do yet")]   FeatureRequest,
    Other,
}

using var client = new JevClient();   // key from TYPESAFE_API_KEY

var res = await client.SystemOneAsync(
    state: "I was charged twice for March and support hasn't replied in four days.",
    questions: new Dictionary<string, Question>
    {
        ["category"] = Question.Choice<Category>("What is this ticket about?"),
        ["urgent"]   = Question.Noul("Does this message express urgency?"),
        ["anger"]    = Question.Score("How angry is the customer?", "calm", "annoyed", "angry", "furious"),
    });

Category category = res.Choice("category").As<Category>();
double confidence = res.Choice("category").Confidence;
double urgency    = res.Noul("urgent").Noul;
double anger      = res.Score("anger").Score;   // 0..3, may be fractional

var queue = (category, confidence) switch
{
    (_, < 0.7)                   => "manual triage",
    (Category.Billing, _)        => "finance",
    (Category.TechnicalIssue, _) => urgency > 0.8 ? "on-call" : "engineering",
    _                            => "general",
};
```

Questions are independent and answered in one round trip, so ask everything about a state together: the
state is tokenized once, and you get one consistent read of the same text.

## Usage

### The three question types

**Noul — yes/no.** Answers with a probability from 0 to 1. Near 1 is yes, near 0 is no, near 0.5 means the
model cannot tell. You pick the threshold.

```csharp
["urgent"] = Question.Noul("Does this message express urgency?")

["complaint"] = Question.Noul(
    "Is this message a complaint?",
    whenTrue:  "The customer is dissatisfied and expects something to change, even if politely phrased",
    whenFalse: "Neutral reports, questions, or feedback offered without expecting a fix")
```

**Choice — pick one.** The answer carries the winning name, a confidence, and the probability of every
option, so you can see when second place was close.

```csharp
["queue"] = Question.Choice("Which queue should handle this?", new Dictionary<string, JsonContent>
{
    ["billing"]      = "Charges, refunds, invoices",
    ["integrations"] = "Third-party connections: Stripe, Shopify, webhooks",
    ["other"]        = default,   // no description: the API reads it by its name
})

// or, when the names speak for themselves:
["tone"] = Question.Choice("What is the customer's tone?", "calm", "frustrated", "angry")
```

**Score — rate on a scale.** The answer is the probability-weighted average across the rubric, so `1.7` means
"mostly level 2, pulled a little towards level 1". The first level is score `0`.

```csharp
["frustration"] = Question.Score("How frustrated is this customer?",
    "calm and matter-of-fact", "mildly annoyed", "clearly frustrated", "angry, threatening to leave")
```

The API accepts at most 255 choice options and 2–10 score levels, and rejects an out-of-range question with a
`JevBadRequestException` explaining which limit was crossed. What this library rejects before the network is
structural: an empty question set, an empty option list, an empty rubric, a raw question with no `type`.

### Choices from enums

Your enum *is* the option list:

```csharp
enum Sentiment
{
    [Description("Happy with the product or the support")] Positive,
    [Description("Neither happy nor unhappy")]             Neutral,
    [Description("Unhappy, complaining, or disappointed")] Negative,
}

["sentiment"] = Question.Choice<Sentiment>("What is the overall sentiment?");
// ...
Sentiment s = res.Choice("sentiment").As<Sentiment>();
```

- Member names travel as snake_case: `TechnicalIssue` → `technical_issue`.
- `[Description]` becomes the option's rubric; without one the option is sent with an explicit `null`.
- `As<TEnum>()` throws `JevResponseValidationException` if the API returns a name that is not a member, which
  beats a silently wrong `default`.

`EnumNames.ToWire` / `EnumNames.FromWire` expose the mapping if you need it elsewhere.

### State, instructions and criteria: `JsonContent`

Everywhere the API takes "text, an object, or an array", this library takes a `JsonContent`. It converts
implicitly from `string` and `JsonNode`; `JsonContent.From(value)` serializes anything else, and takes a
`JsonTypeInfo<T>` for trimmed or AOT apps.

```csharp
await client.SystemOneAsync("plain text", questions);
await client.SystemOneAsync(new JsonObject { ["document"] = "…", ["locale"] = "uk" }, questions);
await client.SystemOneAsync(JsonContent.From(order), questions);
await client.SystemOneAsync(JsonContent.From(order, MyJson.Default.Order), questions);   // AOT-safe
```

Nodes are deep-cloned in and out, so one `JsonObject` can appear in several questions and a question can be
sent any number of times. A default `JsonContent` omits the field; `JsonContent.Null` sends an explicit null.

Rubrics take structure too:

```csharp
var tier = new ChoiceQuestion
{
    Instructions = new JsonObject { ["question"] = "Route the ticket to the right tier." },
    Criteria = new Dictionary<string, JsonContent>
    {
        ["self_serve"] = new JsonObject
        {
            ["what"]     = "Answered by an existing help article",
            ["not_for"]  = "Anything touching the customer's data or money",
            ["examples"] = new JsonArray("how do I change my password"),
        },
    },
};
```

### Raw questions

A `JsonObject` converts implicitly to a `Question` and is sent exactly as given — the escape hatch for fields
or question types the API gains before this library models them. Only its structure is checked; its schema is
left to the API.

```csharp
["experimental"] = new JsonObject { ["type"] = "noul", ["instructions"] = "…", ["new_field"] = 42 }
```

### Reading answers

```csharp
NoulAnswer   n = res.Noul("urgent");     // .Noul            0..1
ChoiceAnswer c = res.Choice("category"); // .Choice, .Confidence, .Probabilities
ScoreAnswer  s = res.Score("anger");     // .Score, .Confidence, .Legend, .Probabilities
```

Asking for the wrong type, or a name that was not in the request, throws `JevResponseValidationException`
rather than returning `null`. The full distribution is always available:

```csharp
foreach (var (name, p) in res.Choice("category").Probabilities.OrderByDescending(x => x.Value))
    Console.WriteLine($"{name,-16} {p:P1}");
```

Also on the response: `res.Model` (the exact version that answered), `res.Usage` (token counts) and
`res.RequestId` (the `x-typesafe-request-id` header — quote it in support requests).

### Typed responses

Derive from `SystemOneResponse` and declare answer-typed properties; each is filled from the answer of the
same name (`[JsonPropertyName]`, else the property name — exact, case-insensitive, then snake_case). Every
such property is required unless it carries `[OptionalAnswer]`.

```csharp
sealed record Ticket : SystemOneResponse
{
    public NoulAnswer Urgent { get; set; } = null!;
    public ChoiceAnswer Category { get; set; } = null!;
    [OptionalAnswer] public ScoreAnswer? Anger { get; set; }
}

var ticket = await client.SystemOneAsync<Ticket>(state, questions);
Category c = ticket.Category.As<Category>();
```

Optional is marked rather than inferred from nullability, because the trimmer removes nullability metadata —
inferring from it would make the same class validate differently in a Native AOT build.

## Configuration

```csharp
using var client = new JevClient(new JevClientOptions
{
    ApiKey         = "sk-...",
    BaseAddress    = new Uri("https://api.typesafe.ai"),
    DefaultModel   = "jev-latest",
    Timeout        = TimeSpan.FromSeconds(10),   // per attempt
    Retry          = RetryPolicy.Default,
    DefaultHeaders = new Dictionary<string, string> { ["X-Team"] = "support-platform" },
    LoggerFactory  = loggerFactory,
});
```

| Option | Default | Environment fallback |
|---|---|---|
| `ApiKey` | — (required) | `TYPESAFE_API_KEY` |
| `BaseAddress` | `https://api.typesafe.ai` | `TYPESAFE_BASE_URL` |
| `DefaultModel` | `jev-latest` | `TYPESAFE_DEFAULT_MODEL` |
| `Timeout` | 10 s per attempt | — |
| `Retry` | `RetryPolicy.Default` | — |
| `DefaultHeaders` | none | — |
| `LoggerFactory` | none — no logging | — |
| `TimeProvider` | `TimeProvider.System` | — |
| `DisposeHttpClient` | `false` for a supplied client | — |

Blank environment variables count as unset. A missing API key throws at construction, not on the first call.
`Authorization`, `Accept`, `Content-Type`, `User-Agent`, `X-TypeSafe-SDK` and `X-TypeSafe-Runtime` are
protected: neither default nor per-call headers can replace them.

Pin a concrete model (`jev-1.13.0`, not `jev-latest`) wherever you have tuned thresholds against its
probabilities.

### Per-call options

```csharp
var res = await client.SystemOneAsync(state, questions, new JevRequestOptions
{
    Model        = "jev-1.13.0",
    Retry        = RetryPolicy.None,
    Timeout      = TimeSpan.FromSeconds(5),
    ExtraHeaders = new Dictionary<string, string> { ["X-Request-Source"] = "batch-job" },
    ExtraBody    = new Dictionary<string, JsonNode?> { ["experimental"] = true },
});
```

`ExtraBody` cannot overwrite `state`, `model` or `questions`.

## Retries and timeouts

`RetryPolicy` defaults mirror the official SDKs:

| | |
|---|---|
| `MaxRetries` | 2 |
| `HttpStatuses` | 408, 429, 500–599 (including the API's `529 overloaded`) |
| `RetryConnectionErrors` / `RetryTimeouts` | `true` |
| `BackoffInitial` → `BackoffMax` | 0.5 s doubling to 5 s |
| `BackoffJitter` | 0.25 subtracted at random |
| `RespectRetryAfter` / `MaxRetryAfter` | `true`, up to 60 s; longer waits fall back to backoff |
| `Budget` | 30 s for the whole call, waits included |
| `Predicate` | your last word — it can only narrow the rules above |

`400`, `401`, `403`, `404` and `422` are never retried. `Timeout` is per attempt, so with two retries one call
can span three timeouts plus backoff; `Budget` is the ceiling on the lot, and a retry whose wait would exceed
it is not taken.

`RetryPolicy.None` turns retrying off. For pluggable policies, pass your own `HttpClient` built with
`Microsoft.Extensions.Http.Resilience` and set `Retry = RetryPolicy.None`.

## Errors

Everything derives from `JevException`:

| Exception | When |
|---|---|
| `JevBadRequestException` | 400 |
| `JevAuthenticationException` | 401 — missing or invalid key |
| `JevPermissionDeniedException` | 403 |
| `JevNotFoundException` | 404 |
| `JevUnprocessableEntityException` | 422 — validation |
| `JevRateLimitException` | 429 — exposes `RetryAfter` |
| `JevInternalServerException` | 5xx, including 529 |
| `JevConnectionException` | no HTTP response at all |
| `JevTimeoutException` | one attempt exceeded `Timeout`; exposes `Timeout` |
| `JevResponseValidationException` | a 2xx whose body is wrong; `FieldPath` names the field |

Every `JevApiException` carries `Status`, the raw `Body`, `RequestId` and `Endpoint`. `Message` reads
`POST https://api.typesafe.ai/v1/systemone: 429 slow down (request_id=…)`, and the endpoint never carries
credentials or a query string.

```csharp
try
{
    var res = await client.SystemOneAsync(state, questions);
}
catch (JevRateLimitException ex)          // retries already exhausted
{
    logger.LogWarning("Rate limited, server asked for {Delay}", ex.RetryAfter);
}
catch (JevApiException ex)
{
    logger.LogError("Jev returned {Status}, request {RequestId}: {Body}", ex.Status, ex.RequestId, ex.Body);
}
```

## Cancellation

The token covers the whole call, retries and waits included:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var res = await client.SystemOneAsync(state, questions, cancellationToken: cts.Token);
```

Cancelling raises `OperationCanceledException` and is never retried; a per-attempt timeout raises
`JevTimeoutException`. The two are kept apart on purpose.

## Logging

Pass a `LoggerFactory` and the client logs one Information line per attempt, with headers and bodies at
Debug. Credential-bearing headers — and any header whose name contains `token`, `secret` or `key` — are
redacted. **Bodies are not**: they are whatever state you sent. Do not enable Debug where that matters.

## Observability

Traces and metrics go through `ActivitySource` and `Meter`, which ship in the .NET runtime, so OpenTelemetry
support costs no package reference. With nobody listening, the client skips the bookkeeping.

```csharp
services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(JevTelemetry.ActivitySourceName))
    .WithMetrics(m => m.AddMeter(JevTelemetry.MeterName));
```

One span covers one call *including its retries* (`http.request.resend_count`).

| Instrument | |
|---|---|
| `typesafe_jev.client.request.duration` | Histogram, seconds — the whole call, waits included |
| `typesafe_jev.client.retries` | Counter — attempts after the first |
| `typesafe_jev.client.token.usage` | Counter — tagged `input` / `output` and the answering model |

No content is recorded — no state, questions, answers or headers. What is recorded is what you configured:
the model you asked for, and your base URL's host and path.

## Dependency injection

There is no `AddJevClient()` package; registering the client is a few lines you can read, and the client is
thread-safe and cheap to construct.

```csharp
// A singleton on the library's own handler — the one to reach for.
services.AddSingleton<IJevClient>(sp => new JevClient(new JevClientOptions
{
    ApiKey = sp.GetRequiredService<IConfiguration>()["TypeSafe:ApiKey"],
    LoggerFactory = sp.GetService<ILoggerFactory>(),
}));
```

Through `IHttpClientFactory`, when you want your host's handlers, register it **transient** — a singleton
would hold one `HttpClient` forever and the factory could never rotate the handler underneath it:

```csharp
services.AddHttpClient("jev");
services.AddTransient<IJevClient>(sp => new JevClient(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("jev"),
    new JevClientOptions
    {
        ApiKey = sp.GetRequiredService<IConfiguration>()["TypeSafe:ApiKey"],
        DisposeHttpClient = false,     // the factory owns the handler
        LoggerFactory = sp.GetService<ILoggerFactory>(),
    }));
```

## Testing

`JevClient` implements `IJevClient`, so your code can take the interface and your tests can hand it a fake.
To test at the HTTP level instead — exercising encoding, retries and decoding for real — give the real client
your own `HttpMessageHandler`:

```csharp
using var client = new JevClient(new HttpClient(myHandler), new JevClientOptions { ApiKey = "test" });
```

Both work without an API key and without a network, so a test suite that uses this library stays offline.

## Examples

Fifteen runnable examples live in [`examples/TypeSafe.Jev.Examples`](examples/TypeSafe.Jev.Examples), covering
all three question types, enum choices, structured state and rubrics, confidence-gated routing, error
handling, configuration, parallel fan-out, cancellation and taxonomy walking.

```bash
dotnet run --project examples/TypeSafe.Jev.Examples            # list them
dotnet run --project examples/TypeSafe.Jev.Examples -- 03      # run one
dotnet run --project examples/TypeSafe.Jev.Examples -- all     # run all
```

They can be run **offline, without an API key**, so you can try the library out before signing up for
anything. Set `TYPESAFE_API_KEY` to call the real API, and pass `--offline` to go back to offline mode. See
the [examples README](examples/TypeSafe.Jev.Examples/README.md) for details.

## Building from source

```bash
git clone https://github.com/RavenValentin/TypeSafe.Jev.git
cd TypeSafe.Jev
dotnet build -c Release
dotnet test
```

Requires the .NET 10 SDK, which builds all three target frameworks. Open `TypeSafe.Jev.sln` in Visual Studio
2026 or later, or in JetBrains Rider or VS Code with the C# extension.

```
src/TypeSafe.Jev/
  Client/       IJevClient, JevClient, JevClientOptions, JevRequestOptions, RetryPolicy
  Questions/    Question + Noul/Choice/Score/Raw questions, EnumNames
  Answers/      Answer + Noul/Choice/Score answers, AnswerConverter
  Models/       SystemOneResponse, AnswerBinder, Usage, ModelCard
  Json/         JsonContent, JevJson, the source-generated JSON context
  Diagnostics/  JevTelemetry, JevDefaults, Log
  Exceptions/   JevException, JevApiException, status-specific exceptions
tests/TypeSafe.Jev.Tests.Unit/    unit tests, on all three target frameworks
tests/TypeSafe.Jev.AotSmoke/      published as a native binary and run in CI
examples/TypeSafe.Jev.Examples/   15 runnable examples
scripts/                          the guards CI runs, and the changeset tool
```

Everything lives in the single `TypeSafe.Jev` namespace — the folders organise the source, not the API
surface, so one `using TypeSafe.Jev;` is all a consumer needs.

## Contributing

Issues and pull requests are welcome.

- Keep the library at one dependency; `scripts/check-dependencies.sh` enforces it.
- Add a test for anything that touches request or response shaping.
- Describe user-visible changes with `scripts/changeset.sh new <major|minor|patch> "…"`.
- `dotnet build -c Release` treats warnings as errors, including the trim and AOT analyzers.

The wire format is documented at [docs.typesafe.ai](https://docs.typesafe.ai): `POST /v1/systemone` and
`GET /v1/models`.

### Releasing

```bash
scripts/changeset.sh release          # bumps the version, moves the notes into CHANGELOG.md
git commit -am "Release 0.2.0"
git tag v0.2.0 && git push --follow-tags
```

The tag is what publishes: `release.yml` builds, tests, runs the AOT smoke check, packs, pushes to
nuget.org and creates the GitHub release. There is no API key to manage — it authenticates through
[NuGet Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing), whose policy
must name this repository and `release.yml`.

## Changelog

See [CHANGELOG.md](CHANGELOG.md). Releases follow [semantic versioning](https://semver.org).

## Roadmap

- [x] Core client, typed questions and answers, exception hierarchy
- [x] `RetryPolicy`, per-call options, logging, OpenTelemetry
- [x] `net8.0` / `net9.0` / `net10.0`, trim- and AOT-safe, CI and release automation
- [ ] `Microsoft.Extensions.AI` integration
- [ ] Opt-in live integration tests against the real API

## License

MIT — see [LICENSE](LICENSE).

Jev and TypeSafe are products of TypeSafe AI. This is an independent community project, not affiliated with
or endorsed by them.
