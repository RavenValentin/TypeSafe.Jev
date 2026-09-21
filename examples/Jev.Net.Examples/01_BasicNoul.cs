namespace Jev.Net.Examples;

/// <summary>
/// The smallest useful call: one yes/no question about a piece of text.
/// A noul answers with a probability, not a boolean, so you pick the threshold.
/// </summary>
public static class BasicNoul
{
    public static async Task RunAsync(IJevClient client)
    {
        const string message = "I've been trying to connect my Stripe account for 3 days and I'm losing sales. Please help ASAP.";

        var res = await client.SystemOneAsync(message, new Dictionary<string, Question>
        {
            ["urgent"] = Question.Noul("Does this message express urgency?"),
        });

        double p = res.Noul("urgent").Noul;

        Console.WriteLine($"urgency: {p:P0}");
        Console.WriteLine(p switch
        {
            > 0.8 => "-> escalate now",
            > 0.5 => "-> normal queue",
            _ => "-> no rush",
        });
    }
}
