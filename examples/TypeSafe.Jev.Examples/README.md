# TypeSafe.Jev examples

Fifteen small programs covering everything the library does. They run **with or without an API key**, so you
can try the library out before signing up for anything.

## Run them

From the repository root:

```bash
# list the examples
dotnet run --project examples/TypeSafe.Jev.Examples

# run one
dotnet run --project examples/TypeSafe.Jev.Examples -- 03

# run all of them
dotnet run --project examples/TypeSafe.Jev.Examples -- all
```

Requires the .NET 10 SDK. Nothing else to install, no configuration file to create.

## Against the real API

```bash
# bash / zsh
export TYPESAFE_API_KEY=sk-...

# PowerShell
$env:TYPESAFE_API_KEY = "sk-..."

# cmd
set TYPESAFE_API_KEY=sk-...
```

With the variable set, the examples call `https://api.typesafe.ai` for real. Pass `--offline` to run them
without spending tokens:

```bash
dotnet run --project examples/TypeSafe.Jev.Examples -- all --offline
```

Get a key at [typesafe.ai](https://typesafe.ai). Other variables the client honours: `TYPESAFE_BASE_URL`,
`TYPESAFE_DEFAULT_MODEL`.

## Offline mode

Without an API key the examples run offline: requests are answered locally instead of over the network, so
the library runs end to end and you can see the code work. The probabilities are not a real model's
judgment — use a key for that.

The same approach works in your own tests: give `JevClient` your own `HttpMessageHandler`.

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
| 10 | [`10_ClientConfiguration.cs`](10_ClientConfiguration.cs) | Timeouts, retries, per-call options, pinned models |
| 11 | [`11_ListModels.cs`](11_ListModels.cs) | `GET /v1/models` |
| 12 | [`12_ParallelFanOut.cs`](12_ParallelFanOut.cs) | Many states at once, kept under the rate limit |
| 13 | [`13_Cancellation.cs`](13_Cancellation.cs) | Cancellation token vs. per-attempt timeout |
| 14 | [`14_TaxonomyWalk.cs`](14_TaxonomyWalk.cs) | Classify down a tree, stop when confidence drops |
| 15 | [`15_NoulBoundaries.cs`](15_NoulBoundaries.cs) | Moving a decision boundary with criteria |
