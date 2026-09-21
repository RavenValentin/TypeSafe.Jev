using System.Globalization;
using Jev.Net;
using Jev.Net.Examples;

// Probabilities read the same on every machine.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

// Run one:      dotnet run --project examples/Jev.Net.Examples -- 03
// Run all:      dotnet run --project examples/Jev.Net.Examples -- all
// No API key?   add --offline (or just leave TYPESAFE_API_KEY unset) and answers are synthesized locally.
var examples = new (string Id, string Title, Func<IJevClient, Task> Run)[]
{
    ("01", "Noul: a single yes/no question", BasicNoul.RunAsync),
    ("02", "Choice: options from a dictionary", ChoiceFromOptions.RunAsync),
    ("03", "Choice: options from an enum with [Description]", ChoiceFromEnum.RunAsync),
    ("04", "Score: rating against a rubric", ScoreRubric.RunAsync),
    ("05", "Several questions in one call", MultipleQuestions.RunAsync),
    ("06", "Structured state: send an object, not a string", StructuredState.RunAsync),
    ("07", "Structured instructions and rubrics as JSON", StructuredInstructions.RunAsync),
    ("08", "Confidence-gated routing", ConfidenceRouting.RunAsync),
    ("09", "Handling every error type", ErrorHandling.RunAsync),
    ("10", "Client configuration: timeouts, retries, base URL", ClientConfiguration.RunAsync),
    ("11", "Listing available models", ListModels.RunAsync),
    ("12", "Speculative fan-out over many inputs", ParallelFanOut.RunAsync),
    ("13", "Cancellation", Cancellation.RunAsync),
    ("14", "Taxonomy walk: classify down a tree", TaxonomyWalk.RunAsync),
    ("15", "Noul criteria: pinning down what true and false mean", NoulBoundaries.RunAsync),
};

var selection = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
var hasKey = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TYPESAFE_API_KEY"));
ExampleClients.Offline = args.Contains("--offline") || !hasKey;

if (selection is null)
{
    Console.WriteLine("Examples:\n");
    foreach (var (id, title, _) in examples) Console.WriteLine($"  {id}  {title}");
    Console.WriteLine("\nUsage: dotnet run --project examples/Jev.Net.Examples -- <id|all> [--offline]");
    Console.WriteLine(hasKey
        ? "\nTYPESAFE_API_KEY is set: examples call the real API. Add --offline to avoid spending tokens."
        : "\nTYPESAFE_API_KEY is not set: examples run offline against a local fake.");
    return 0;
}

var toRun = selection is "all" ? examples : examples.Where(e => e.Id == selection).ToArray();
if (toRun.Length == 0)
{
    Console.Error.WriteLine($"No example '{selection}'. Run without arguments to list them.");
    return 1;
}

// Offline mode answers from the request itself, so the whole library runs end to end —
// encoding, retries, decoding, typed accessors — without a key, a network, or a bill.
if (ExampleClients.Offline)
    Console.WriteLine("[offline] answers are synthesized locally; set TYPESAFE_API_KEY to call the real API.");

using var client = ExampleClients.Create();

foreach (var (id, title, run) in toRun)
{
    Console.WriteLine($"\n=== {id}  {title} ===");
    await run(client);
}

return 0;
