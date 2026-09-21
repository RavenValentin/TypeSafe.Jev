# Jev.Net

[![NuGet](https://img.shields.io/nuget/v/Jev.Net.svg)](https://www.nuget.org/packages/Jev.Net)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Unofficial .NET client for **[Jev](https://docs.typesafe.ai)**, TypeSafe AI's System One decision model.

TypeSafe ships official SDKs for Python and TypeScript. This is the missing one for .NET.

Jev is not a chat model. You give it some state and a set of typed questions; it answers each one with
a **value plus a probability distribution and a confidence score**. There is no prose to parse, no JSON
to coax out of a prompt, and no "sometimes it wraps the answer in a code fence" — the response is
already the shape your `switch` statement wants.

```csharp
var res = await client.SystemOneAsync(ticket, new Dictionary<string, Question>
{
    ["category"] = Question.Choice<Category>("What is this ticket about?"),
    ["urgent"]   = Question.Noul("Is the customer in a hurry?"),
});

Category category = res.Choice("category").As<Category>();   // a real enum
double urgency    = res.Noul("urgent").Noul;                 // 0..1
```

---

## Contents

- [Install](#install)
- [Quickstart](#quickstart)
- [The three question types](#the-three-question-types)
  - [Noul — yes/no](#noul--yesno)
  - [Choice — pick one](#choice--pick-one)
  - [Score — rate on a scale](#score--rate-on-a-scale)
- [Choices from enums](#choices-from-enums)
- [State: text or structure](#state-text-or-structure)
- [Structured instructions and rubrics](#structured-instructions-and-rubrics)
- [Reading answers](#reading-answers)
- [Configuration](#configuration)
- [Retries and timeouts](#retries-and-timeouts)
- [Errors](#errors)
- [Cancellation](#cancellation)
- [Listing models](#listing-models)
- [Examples](#examples)
- [Project layout](#project-layout)
- [Roadmap](#roadmap)
- [Contributing](#contributing)
- [License](#license)

---

## Install

```bash
dotnet add package Jev.Net
```

Targets `net8.0` and `net9.0`. No third-party dependencies — just `HttpClient` and `System.Text.Json`.

Get an API key from [typesafe.ai](https://typesafe.ai) and put it in `TYPESAFE_API_KEY`.

---

## Quickstart

```csharp
using System.ComponentModel;
using Jev.Net;

enum Category
{
    [Description("Charges, refunds, invoices, subscription changes")] Billing,
    [Description("Bugs, outages, broken integrations")]               TechnicalIssue,
    [Description("Asking for something the product cannot do yet")]   FeatureRequest,
    Other,
}

using var client = new JevClient();   // reads TYPESAFE_API_KEY

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
    (_, < 0.7)                    => "manual triage",
    (Category.Billing, _)         => "finance",
    (Category.TechnicalIssue, _)  => urgency > 0.8 ? "on-call" : "engineering",
    _                             => "general",
};
```

One call, three questions, one tokenization of the state. Asking five questions about the same state
in one request is far cheaper than five requests — and gives you one consistent read of the same text.

---

## The three question types

Build them with the factories on `Question`, or construct the records directly when you need JSON
structure (see [Structured instructions](#structured-instructions-and-rubrics)).

### Noul — yes/no

Answers with a probability from 0 to 1. Near 1 means yes, near 0 means no, near 0.5 means the model
genuinely cannot tell. You pick the threshold; nothing forces you to collapse it to a `bool`.

```csharp
["urgent"] = Question.Noul("Does this message express urgency?")
```

Optionally pin down where the line sits:

```csharp
["complaint"] = Question.Noul(
    "Is this message a complaint?",
    whenTrue:  "The customer is dissatisfied and expects something to change, even if politely phrased",
    whenFalse: "Neutral reports, questions, or feedback offered without expecting a fix")
```

### Choice — pick one

Up to 255 named options. The answer carries the winning name, a confidence, and the probability of
every option — so you can see when second place was close.

```csharp
["queue"] = Question.Choice("Which queue should handle this?", new Dictionary<string, string?>
{
    ["billing"]      = "Charges, refunds, invoices",
    ["integrations"] = "Third-party connections: Stripe, Shopify, webhooks",
    ["other"]        = null,   // null means "the name speaks for itself"
})
```

### Score — rate on a scale

2–10 ordered levels. The answer is the probability-weighted average, so `1.7` means "mostly level 2,
pulled a little towards level 1" — more information than any single level could carry.

```csharp
["frustration"] = Question.Score("How frustrated is this customer?",
    "calm and matter-of-fact",
    "mildly annoyed",
    "clearly frustrated",
    "angry, threatening to leave")
```

The first level is score `0`.

---

## Choices from enums

The typed path, and the reason this library exists. Your enum *is* the option list:

```csharp
enum Sentiment
{
    [Description("Happy with the product or the support")] Positive,
    [Description("Neither happy nor unhappy")]             Neutral,
    [Description("Unhappy, complaining, or disappointed")]  Negative,
}

["sentiment"] = Question.Choice<Sentiment>("What is the overall sentiment?")
// ...
Sentiment s = res.Choice("sentiment").As<Sentiment>();
```

- Member names travel as snake_case: `TechnicalIssue` → `technical_issue`.
- `[Description]` becomes the option's rubric. Without one, the option is sent with a `null` description.
- `As<TEnum>()` maps the answer back, and throws `JevResponseValidationException` if the API ever
  returns a name that is not a member — which beats a silently wrong `default` value.

Add a member to the enum and both the request and your `switch` update together; the compiler tells you
about the branch you forgot.

Need the raw name instead? `res.Choice("sentiment").Choice` is the string. `EnumNames.ToWire` /
`EnumNames.FromWire` expose the mapping if you need it elsewhere.

---

## State: text or structure

State can be a string, an object, an array, or `null` — anything serializable. Objects are sent as JSON,
so the model sees labelled fields instead of a paragraph you had to template by hand:

```csharp
var order = new
{
    id = "ORD-91821",
    total = 2340.00m,
    country = "MT",
    previous_orders = 0,
    billing_matches_shipping = false,
};

var res = await client.SystemOneAsync(order, new Dictionary<string, Question>
{
    ["fraud"] = Question.Noul("Does this order look fraudulent?"),
});
```

Budget: 64k tokens per request, of which 32k for the state plus the longest question.

---

## Structured instructions and rubrics

Instructions, choice options and score levels all accept JSON, not just strings. Use it when a rubric
needs structure of its own — what a level means, which signals to look for, what it explicitly excludes.
Construct the question records directly:

```csharp
var tier = new ChoiceQuestion
{
    Instructions = new JsonObject
    {
        ["field"]    = new JsonObject { ["name"] = "support_tier", ["type"] = "string" },
        ["question"] = "Route the ticket to the right tier.",
    },
    Criteria = new Dictionary<string, JsonNode?>
    {
        ["self_serve"] = new JsonObject
        {
            ["what"]     = "Answered by an existing help article",
            ["not_for"]  = "Anything touching the customer's data or money",
            ["examples"] = new JsonArray("how do I change my password", "where are my invoices"),
        },
        ["engineering"] = new JsonObject
        {
            ["what"]     = "Needs code, logs, or a deploy",
            ["examples"] = new JsonArray("webhooks stopped firing", "500 on checkout"),
        },
    },
};
```

Score levels work the same way, as an array of objects with `summary` and `signals`. See
[example 07](examples/Jev.Net.Examples/07_StructuredInstructions.cs).

---

## Reading answers

`SystemOneResponse` gives you typed accessors that fail loudly when the name or type is wrong:

```csharp
NoulAnswer   n = res.Noul("urgent");     // .Noul            0..1
ChoiceAnswer c = res.Choice("category"); // .Choice, .Confidence, .Probabilities
ScoreAnswer  s = res.Score("anger");     // .Score, .Confidence, .Legend, .Probabilities
```

Asking for the wrong type, or a name that was not in the request, throws
`JevResponseValidationException` rather than returning `null`.

The full distribution is always there when you want it:

```csharp
foreach (var (name, p) in res.Choice("category").Probabilities.OrderByDescending(x => x.Value))
    Console.WriteLine($"{name,-16} {p:P1}");

var score = res.Score("anger");
foreach (var (level, p) in score.Probabilities.OrderBy(x => x.Key))
    Console.WriteLine($"{level} {score.Legend[level]} {p:P1}");
```

Also on the response: `res.Model` (the exact version that answered), `res.Usage` (token counts) and
`res.RequestId` (the `x-typesafe-request-id` header — quote it in support requests).

---

## Configuration

```csharp
using var client = new JevClient(new JevClientOptions
{
    ApiKey        = "sk-...",                          // default: TYPESAFE_API_KEY
    BaseAddress   = new Uri("https://api.typesafe.ai"), // default: TYPESAFE_BASE_URL, then this
    DefaultModel  = "jev-latest",                       // default: TYPESAFE_DEFAULT_MODEL, then this
    Timeout       = TimeSpan.FromSeconds(10),           // per attempt
    MaxRetries    = 2,
    MaxRetryAfter = TimeSpan.FromSeconds(60),
});
```

| Option | Default | Environment fallback |
|---|---|---|
| `ApiKey` | — (required) | `TYPESAFE_API_KEY` |
| `BaseAddress` | `https://api.typesafe.ai` | `TYPESAFE_BASE_URL` |
| `DefaultModel` | `jev-latest` | `TYPESAFE_DEFAULT_MODEL` |
| `Timeout` | 10 s per attempt | — |
| `MaxRetries` | 2 | — |
| `MaxRetryAfter` | 60 s | — |

A missing API key throws at construction, not on the first call.

Bring your own `HttpClient` — from `IHttpClientFactory`, with your own handlers, proxy or logging:

```csharp
using var client = new JevClient(httpClient, new JevClientOptions { MaxRetries = 0 });
```

The client sets its own per-attempt timeout internally, so leave `HttpClient.Timeout` alone.

Override the model for a single call:

```csharp
await client.SystemOneAsync(state, questions, model: "jev-1.13.0");
```

Pin a concrete version when you need reproducible answers; `jev-latest` moves when TypeSafe ships.

---

## Retries and timeouts

Defaults mirror the official SDKs, so behaviour matches what the Python and TypeScript docs describe:

- **Retried:** `408`, `429`, all `5xx` (including `529 overloaded`), connection failures, and per-attempt timeouts.
- **Not retried:** `400`, `401`, `403`, `404`, `422` — retrying a malformed request just wastes a call.
- **Backoff:** 500 ms, doubling to a 5 s ceiling, with 25 % jitter subtracted.
- **`Retry-After`:** honoured (both `Retry-After` and `retry-after-ms`) up to `MaxRetryAfter`; longer waits fall back to backoff.
- **`Timeout` is per attempt.** With `MaxRetries = 2` a single call can span three attempts plus backoff.
  Use a `CancellationToken` to bound the whole thing.

No Polly dependency — the policy is about thirty lines against `HttpClient`. If you need pluggable
policies, pass your own `HttpClient` built with `Microsoft.Extensions.Http.Resilience` and set
`MaxRetries = 0`.

---

## Errors

Everything derives from `JevException`:

| Exception | Status | Notes |
|---|---|---|
| `JevBadRequestException` | 400 | Malformed request |
| `JevAuthenticationException` | 401 | Missing or invalid API key |
| `JevPermissionDeniedException` | 403 | Key not allowed to do this |
| `JevNotFoundException` | 404 | Unknown endpoint or resource |
| `JevUnprocessableEntityException` | 422 | Validation: too many options, too few score levels, … |
| `JevRateLimitException` | 429 | Exposes `RetryAfter` |
| `JevInternalServerException` | 5xx | Including `529` overloaded |
| `JevConnectionException` | — | No HTTP response at all |
| `JevTimeoutException` | — | One attempt exceeded `Timeout`; exposes `Timeout` |
| `JevResponseValidationException` | — | 2xx with an unexpected shape; exposes `FieldPath` |

Every `JevApiException` carries `Status`, the raw `Body`, and `RequestId`.

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

---

## Cancellation

The token covers the whole call, retries and backoff included:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var res = await client.SystemOneAsync(state, questions, cancellationToken: cts.Token);
```

Cancelling raises `OperationCanceledException`; a per-attempt timeout raises `JevTimeoutException`.
The two are kept distinct on purpose — one is your decision, the other is the network's.

---

## Listing models

```csharp
foreach (var m in await client.ListModelsAsync())
    Console.WriteLine($"{m.Name}  {m.ReleaseDate}  {m.Description}");
```

---

## Examples

Fifteen runnable examples live in [`examples/Jev.Net.Examples`](examples/Jev.Net.Examples):

```bash
export TYPESAFE_API_KEY=sk-...
dotnet run --project examples/Jev.Net.Examples            # list them
dotnet run --project examples/Jev.Net.Examples -- 03      # run one
dotnet run --project examples/Jev.Net.Examples -- all     # run all
```

| # | Example | Shows |
|---|---|---|
| 01 | [Basic noul](examples/Jev.Net.Examples/01_BasicNoul.cs) | The smallest useful call |
| 02 | [Choice from options](examples/Jev.Net.Examples/02_ChoiceFromOptions.cs) | Options built at runtime |
| 03 | [Choice from enum](examples/Jev.Net.Examples/03_ChoiceFromEnum.cs) | `[Description]` rubrics, typed answers |
| 04 | [Score rubric](examples/Jev.Net.Examples/04_ScoreRubric.cs) | Ordered levels and the full distribution |
| 05 | [Multiple questions](examples/Jev.Net.Examples/05_MultipleQuestions.cs) | Five questions, one round trip |
| 06 | [Structured state](examples/Jev.Net.Examples/06_StructuredState.cs) | Objects instead of templated strings |
| 07 | [Structured instructions](examples/Jev.Net.Examples/07_StructuredInstructions.cs) | JSON rubrics with `what` / `not_for` / `examples` |
| 08 | [Confidence routing](examples/Jev.Net.Examples/08_ConfidenceRouting.cs) | Automate the confident cases, escalate the rest |
| 09 | [Error handling](examples/Jev.Net.Examples/09_ErrorHandling.cs) | Every exception type and what to do about it |
| 10 | [Client configuration](examples/Jev.Net.Examples/10_ClientConfiguration.cs) | Timeouts, retries, custom `HttpClient`, pinned models |
| 11 | [List models](examples/Jev.Net.Examples/11_ListModels.cs) | `GET /v1/models` |
| 12 | [Parallel fan-out](examples/Jev.Net.Examples/12_ParallelFanOut.cs) | Many states at once, under the rate limit |
| 13 | [Cancellation](examples/Jev.Net.Examples/13_Cancellation.cs) | Token vs. per-attempt timeout |
| 14 | [Taxonomy walk](examples/Jev.Net.Examples/14_TaxonomyWalk.cs) | Classify down a tree, stop when unsure |
| 15 | [Noul boundaries](examples/Jev.Net.Examples/15_NoulBoundaries.cs) | Moving the decision boundary with criteria |

---

## Project layout

```
src/Jev.Net/
  Client/       IJevClient, JevClient, JevClientOptions
  Questions/    Question + Noul/Choice/Score questions, EnumNames
  Answers/      Answer + Noul/Choice/Score answers, AnswerConverter
  Models/       SystemOneResponse, Usage, ModelCard
  Exceptions/   JevException, JevApiException, status-specific exceptions
tests/Jev.Net.Tests.Unit/     xUnit tests over a fake HttpMessageHandler
examples/Jev.Net.Examples/    15 runnable examples
```

Everything lives in the single `Jev.Net` namespace — the folders organise the source, not the API surface,
so one `using Jev.Net;` is all a consumer needs.

Build and test:

```bash
dotnet build -c Release
dotnet test
```

---

## Roadmap

- [x] Core client, typed questions and answers, exception hierarchy, retries
- [ ] `Jev.Net.DependencyInjection` — `services.AddJevClient(...)` over `IHttpClientFactory`
- [ ] CI: build, test, and publish to NuGet on tag
- [ ] `Jev.Net.Extensions.AI` — `Microsoft.Extensions.AI` integration
- [ ] Source-generated `JsonSerializerContext` for Native AOT

---

## Contributing

Issues and pull requests are welcome. Please keep the dependency count at zero for the core package,
and add a test for anything that touches request or response shaping.

The wire format this client targets is documented at [docs.typesafe.ai](https://docs.typesafe.ai) —
`POST /v1/systemone` and `GET /v1/models`. If the API changes, start there.

## License

MIT — see [LICENSE](LICENSE).

Jev and TypeSafe are products of TypeSafe AI; this library is an independent community project and is
not affiliated with or endorsed by them.
