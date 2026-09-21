using System.ComponentModel;

namespace TypeSafe.Jev.Examples;

/// <summary>
/// Ask everything you need about one state in a single round trip. The state is tokenized once,
/// so five questions cost far less than five calls — and you get one consistent read of the same text.
/// </summary>
public static class MultipleQuestions
{
    private enum Sentiment
    {
        [Description("Happy with the product or the support")] Positive,
        [Description("Neither happy nor unhappy")] Neutral,
        [Description("Unhappy, complaining, or disappointed")] Negative,
    }

    public static async Task RunAsync(IJevClient client)
    {
        const string review = """
            Support got back to me in ten minutes, which was great. The fix worked.
            But this is the second outage this month and I'm starting to wonder about reliability.
            """;

        var res = await client.SystemOneAsync(review, new Dictionary<string, Question>
        {
            ["sentiment"] = Question.Choice<Sentiment>("What is the overall sentiment?"),
            ["churn_risk"] = Question.Noul("Is this customer at risk of cancelling?"),
            ["mentions_outage"] = Question.Noul("Does the text mention an outage or downtime?"),
            ["severity"] = Question.Score("How severe is the underlying problem?", "cosmetic", "annoying", "blocking", "business-critical"),
            ["needs_follow_up"] = Question.Noul("Should a human follow up on this?"),
        });

        Console.WriteLine($"sentiment:       {res.Choice("sentiment").As<Sentiment>()}");
        Console.WriteLine($"churn risk:      {res.Noul("churn_risk").Noul:P0}");
        Console.WriteLine($"mentions outage: {res.Noul("mentions_outage").Noul:P0}");
        Console.WriteLine($"severity:        {res.Score("severity").Score:0.0}");
        Console.WriteLine($"follow up:       {res.Noul("needs_follow_up").Noul:P0}");
        Console.WriteLine($"tokens in:       {res.Usage?.InputTokens}");
    }
}
