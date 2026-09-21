namespace Jev.Net.Examples;

/// <summary>
/// Defaults match the official SDKs, but everything is adjustable. Note that the timeout is
/// per attempt, not per call: with MaxRetries = 2 a call can take up to three timeouts plus backoff.
/// </summary>
public static class ClientConfiguration
{
    public static async Task RunAsync(IJevClient _)
    {
        // A client that owns its HttpClient.
        using var tuned = new JevClient(new JevClientOptions
        {
            // ApiKey defaults to TYPESAFE_API_KEY.
            BaseAddress = new Uri("https://api.typesafe.ai"),
            DefaultModel = "jev-latest",
            Timeout = TimeSpan.FromSeconds(30),   // long states need more than the 10s default
            MaxRetries = 4,
            MaxRetryAfter = TimeSpan.FromSeconds(20),
        });

        var res = await tuned.SystemOneAsync("A short sentence.", new Dictionary<string, Question>
        {
            ["short"] = Question.Noul("Is this text under twenty words?"),
        });
        Console.WriteLine($"tuned client: {res.Noul("short").Noul:P0}, answered by {res.Model}");

        // Or bring your own HttpClient — from IHttpClientFactory, with your own handlers,
        // proxy, or logging. The client sets its own per-attempt timeout, so leave HttpClient.Timeout alone.
        using var http = new HttpClient();
        using var byoc = new JevClient(http, new JevClientOptions { MaxRetries = 0 });

        // Pin a specific model version for reproducibility, per call.
        var pinned = await byoc.SystemOneAsync("A short sentence.", new Dictionary<string, Question>
        {
            ["short"] = Question.Noul("Is this text under twenty words?"),
        }, model: "jev-1.13.0");
        Console.WriteLine($"pinned model: {pinned.Model}");
    }
}
