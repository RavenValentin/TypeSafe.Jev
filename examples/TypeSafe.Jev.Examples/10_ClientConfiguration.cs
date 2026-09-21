namespace TypeSafe.Jev.Examples;

/// <summary>
/// Defaults match the official SDKs, but everything is adjustable — on the client, or per call.
/// Note that <see cref="JevClientOptions.Timeout"/> is per attempt; the ceiling on the whole call,
/// waits included, is <see cref="RetryPolicy.Budget"/>.
/// </summary>
public static class ClientConfiguration
{
    public static async Task RunAsync(IJevClient _)
    {
        // In your app this line is `new JevClient(options)`; the examples route it through a factory
        // so the same code can also run offline.
        using var tuned = ExampleClients.Create(new JevClientOptions
        {
            // ApiKey defaults to TYPESAFE_API_KEY, BaseAddress to TYPESAFE_BASE_URL.
            DefaultModel = "jev-latest",
            Timeout = TimeSpan.FromSeconds(30),        // per attempt; long states need more than the 10s default
            Retry = RetryPolicy.Default with
            {
                MaxRetries = 4,
                BackoffMax = TimeSpan.FromSeconds(10),
                MaxRetryAfter = TimeSpan.FromSeconds(20),
                Budget = TimeSpan.FromMinutes(1),      // the whole call, retries and waits included
            },
            DefaultHeaders = new Dictionary<string, string> { ["X-Team"] = "support-platform" },
        });

        var question = new Dictionary<string, Question>
        {
            ["short"] = Question.Noul("Is this text under twenty words?"),
        };

        var res = await tuned.SystemOneAsync("A short sentence.", question);
        Console.WriteLine($"tuned client: {res.Noul("short").Noul:P0}, answered by {res.Model}");

        // You can also bring your own HttpClient — from IHttpClientFactory, with your own handlers,
        // proxy or logging:  new JevClient(httpClient, options)
        // The client applies its own per-attempt timeout, so leave HttpClient.Timeout alone.
        using var client = ExampleClients.Create();

        // Per-call overrides beat the client's settings, and only for that call.
        var pinned = await client.SystemOneAsync("A short sentence.", question, new JevRequestOptions
        {
            Model = "jev-1.13.0",                      // pin a version where thresholds are tuned
            Retry = RetryPolicy.None,                  // this one is not worth retrying
            Timeout = TimeSpan.FromSeconds(5),
            ExtraHeaders = new Dictionary<string, string> { ["X-Request-Source"] = "example-10" },
        });

        Console.WriteLine($"pinned model: {pinned.Model}");
    }
}
