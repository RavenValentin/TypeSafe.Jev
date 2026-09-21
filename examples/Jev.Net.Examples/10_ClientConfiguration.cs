namespace Jev.Net.Examples;

/// <summary>
/// Defaults match the official SDKs, but everything is adjustable. Note that the timeout is
/// per attempt, not per call: with MaxRetries = 2 a call can take up to three timeouts plus backoff.
/// </summary>
public static class ClientConfiguration
{
    public static async Task RunAsync(IJevClient _)
    {
        // In your app this line is `new JevClient(options)`; the examples route it through a factory
        // so the same code can also run offline.
        using var tuned = ExampleClients.Create(new JevClientOptions
        {
            // ApiKey defaults to TYPESAFE_API_KEY.
            BaseAddress = new Uri("https://api.typesafe.ai"),
            DefaultModel = "jev-latest",
            Timeout = TimeSpan.FromSeconds(30),   // long states need more than the 10s default
            MaxRetries = 4,
            MaxRetryAfter = TimeSpan.FromSeconds(20),
        });

        var question = new Dictionary<string, Question>
        {
            ["short"] = Question.Noul("Is this text under twenty words?"),
        };

        var res = await tuned.SystemOneAsync("A short sentence.", question);
        Console.WriteLine($"tuned client: {res.Noul("short").Noul:P0}, answered by {res.Model}");

        // You can also bring your own HttpClient — from IHttpClientFactory, with your own handlers,
        // proxy or logging:  new JevClient(httpClient, options)
        // The client sets its own per-attempt timeout internally, so leave HttpClient.Timeout alone.
        using var noRetries = ExampleClients.Create(new JevClientOptions { MaxRetries = 0 });

        // Pin a specific model version per call when you need reproducible answers.
        var pinned = await noRetries.SystemOneAsync("A short sentence.", question, model: "jev-1.13.0");
        Console.WriteLine($"pinned model: {pinned.Model}");
    }
}
