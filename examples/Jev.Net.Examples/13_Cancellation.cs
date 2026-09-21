namespace Jev.Net.Examples;

/// <summary>
/// The cancellation token covers the whole call, retries and backoff included — unlike
/// <see cref="JevClientOptions.Timeout"/>, which applies to each attempt separately.
/// Cancelling raises OperationCanceledException; a per-attempt timeout raises JevTimeoutException.
/// The two are kept apart on purpose: one is your decision, the other is the network's.
/// </summary>
public static class Cancellation
{
    public static async Task RunAsync(IJevClient client)
    {
        var question = new Dictionary<string, Question> { ["short"] = Question.Noul("Is this text short?") };

        // A generous budget for the whole call, retries included.
        using var generous = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var res = await client.SystemOneAsync("Any text at all.", question, cancellationToken: generous.Token);
        Console.WriteLine($"finished within budget: {res.Noul("short").Noul:P0}");

        // A token that is already cancelled: the call never leaves the process.
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        try
        {
            await client.SystemOneAsync("Any text at all.", question, cancellationToken: cancelled.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("cancelled token: OperationCanceledException, as expected");
        }
    }
}
