using System.Diagnostics;

namespace Jev.Net.Examples;

/// <summary>
/// Different states cannot share a call, so fan them out. The client is thread-safe;
/// keep the parallelism under your rate limit and let the built-in retries absorb the occasional 429.
/// </summary>
public static class ParallelFanOut
{
    public static async Task RunAsync(IJevClient client)
    {
        string[] passages =
        [
            "The mitochondrion is the powerhouse of the cell.",
            "Our Q3 revenue grew 14% year over year.",
            "Preheat the oven to 200°C and rest the dough for an hour.",
            "The defendant filed a motion to dismiss on jurisdictional grounds.",
            "Kubernetes reschedules pods when a node becomes unreachable.",
        ];

        var question = new Dictionary<string, Question>
        {
            ["relevant"] = Question.Noul("Is this passage about running software in production?"),
        };

        var sw = Stopwatch.StartNew();
        using var limit = new SemaphoreSlim(4); // keep well inside the rate limit

        var scored = await Task.WhenAll(passages.Select(async passage =>
        {
            await limit.WaitAsync();
            try
            {
                var res = await client.SystemOneAsync(passage, question);
                return (passage, score: res.Noul("relevant").Noul);
            }
            finally
            {
                limit.Release();
            }
        }));

        foreach (var (passage, score) in scored.OrderByDescending(x => x.score))
            Console.WriteLine($"{score:P0}  {passage}");

        Console.WriteLine($"\n{passages.Length} calls in {sw.ElapsedMilliseconds} ms");
    }
}
