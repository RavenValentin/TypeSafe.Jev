# Jev.Net examples

Fifteen small programs covering everything the library does. They run **with or without an API key** —
offline the requests are answered by a local fake that reads the questions you actually sent, so you can
check the whole path (encoding → retries → decoding → typed accessors) before spending a token.

## Run them

From the repository root:

```bash
# list the examples
dotnet run --project examples/Jev.Net.Examples

# one example, offline (no key needed)
dotnet run --project examples/Jev.Net.Examples -- 03

# all of them
dotnet run --project examples/Jev.Net.Examples -- all
```

Requires the .NET 9 SDK. Nothing else to install, no configuration file to create.

## Against the real API

```bash
# bash / zsh
export TYPESAFE_API_KEY=sk-...

# PowerShell
$env:TYPESAFE_API_KEY = "sk-..."

# cmd
set TYPESAFE_API_KEY=sk-...
```

With the variable set, the examples call `https://api.typesafe.ai` for real. Add `--offline` to force the
fake back on when you want to re-run something without paying for it:

```bash
dotnet run --project examples/Jev.Net.Examples -- all --offline
```

Get a key at [typesafe.ai](https://typesafe.ai). Other variables the client honours:
`TYPESAFE_BASE_URL`, `TYPESAFE_DEFAULT_MODEL`.

## What is in the box

| # | File | Shows |
|---|---|---|
| 01 | [`01_BasicNoul.cs`](01_BasicNoul.cs) | The smallest useful call: one yes/no question |
| 02 | [`02_ChoiceFromOptions.cs`](02_ChoiceFromOptions.cs) | Options built at runtime from your own data |
| 03 | [`03_ChoiceFromEnum.cs`](03_ChoiceFromEnum.cs) | `[Description]` rubrics, answers as a typed enum |
| 04 | [`04_ScoreRubric.cs`](04_ScoreRubric.cs) | Ordered levels and the full probability distribution |
| 05 | [`05_MultipleQuestions.cs`](05_MultipleQuestions.cs) | Five questions in one round trip |
| 06 | [`06_StructuredState.cs`](06_StructuredState.cs) | Send an object instead of a templated string |
| 07 | [`07_StructuredInstructions.cs`](07_StructuredInstructions.cs) | JSON rubrics with `what` / `not_for` / `examples` |
| 08 | [`08_ConfidenceRouting.cs`](08_ConfidenceRouting.cs) | Automate the confident cases, escalate the rest |
| 09 | [`09_ErrorHandling.cs`](09_ErrorHandling.cs) | Every exception type and what to do about it |
| 10 | [`10_ClientConfiguration.cs`](10_ClientConfiguration.cs) | Timeouts, retries, custom `HttpClient`, pinned models |
| 11 | [`11_ListModels.cs`](11_ListModels.cs) | `GET /v1/models` |
| 12 | [`12_ParallelFanOut.cs`](12_ParallelFanOut.cs) | Many states at once, kept under the rate limit |
| 13 | [`13_Cancellation.cs`](13_Cancellation.cs) | Cancellation token vs. per-attempt timeout |
| 14 | [`14_TaxonomyWalk.cs`](14_TaxonomyWalk.cs) | Classify down a tree, stop when confidence drops |
| 15 | [`15_NoulBoundaries.cs`](15_NoulBoundaries.cs) | Moving a decision boundary with criteria |

## How offline mode works

[`Offline/FakeJevHandler.cs`](Offline/FakeJevHandler.cs) is an `HttpMessageHandler` that parses the outgoing
request, builds a well-formed answer for each question it finds, and returns it. Values are derived from the
state and the question with a stable hash, so repeated runs give the same numbers and changing a rubric
changes the answer.

It exercises the library, not the model — the probabilities are synthetic. Use it to verify that your
questions encode correctly and your code handles the answers; use a real key to find out what Jev thinks.

The same trick works in your own tests: `new JevClient(new HttpClient(yourHandler), options)`.
