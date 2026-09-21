namespace Jev.Net.Examples;

/// <summary>
/// The cancellation token covers the whole call, retries and backoff included — unlike
/// <see cref="JevClientOptions.Timeout"/>, which applies to each attempt separately.
/// Cancelling raises OperationCanceledException, never JevTimeoutException.
/// </summary>
public static class Cancellation
{
    public static async Task RunAsync(IJevClient client)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        try
        {
            var res = await client.SystemOneAsync(
                "Any text at all.",
                new Dictionary<string, Question> { ["q"] = Question.Noul("Is this text short?") },
                cancellationToken: cts.Token);

            Console.WriteLine($"finished in time: {res.Noul("q").Noul:P0}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("cancelled after 50 ms, as expected");
        }
    }
}
