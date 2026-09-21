namespace Jev.Net.Examples;

/// <summary>
/// A choice over options you build at runtime — from a database table, a config file,
/// or anything else you cannot express as an enum. A null description means the name speaks for itself.
/// </summary>
public static class ChoiceFromOptions
{
    public static async Task RunAsync(IJevClient client)
    {
        // Imagine these came from your queues table.
        var queues = new Dictionary<string, JsonContent>
        {
            ["billing"] = "Charges, refunds, invoices, subscription changes",
            ["integrations"] = "Third-party connections: Stripe, Shopify, webhooks",
            ["account"] = "Login, passwords, seats, permissions",
            ["other"] = default,   // no description: the API reads it by its name
        };

        var res = await client.SystemOneAsync(
            "My webhook stopped firing after I rotated the signing secret.",
            new Dictionary<string, Question> { ["queue"] = Question.Choice("Which queue should handle this ticket?", queues) });

        var answer = res.Choice("queue");
        Console.WriteLine($"queue: {answer.Choice} (confidence {answer.Confidence:P0})");

        foreach (var (name, p) in answer.Probabilities.OrderByDescending(entry => entry.Value))
            Console.WriteLine($"  {name,-14} {p:P1}");
    }
}
