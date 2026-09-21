using Jev.Net;
using Jev.Net.Examples;

// Every example takes a client and prints what it got back.
// Run one:  dotnet run --project examples/Jev.Net.Examples -- 03
// Run all:  dotnet run --project examples/Jev.Net.Examples -- all
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

if (Environment.GetEnvironmentVariable("TYPESAFE_API_KEY") is null)
{
    Console.Error.WriteLine("Set TYPESAFE_API_KEY first. Get a key at https://typesafe.ai.");
    return 1;
}

var selected = args.Length == 0 ? null : args[0];
if (selected is null)
{
    Console.WriteLine("Examples:\n");
    foreach (var (id, title, _) in examples) Console.WriteLine($"  {id}  {title}");
    Console.WriteLine("\nPass an id, or 'all'.");
    return 0;
}

using var client = new JevClient();
var toRun = selected is "all" ? examples : examples.Where(e => e.Id == selected).ToArray();
if (toRun.Length == 0)
{
    Console.Error.WriteLine($"No example '{selected}'.");
    return 1;
}

foreach (var (id, title, run) in toRun)
{
    Console.WriteLine($"\n=== {id}  {title} ===");
    await run(client);
}

return 0;
