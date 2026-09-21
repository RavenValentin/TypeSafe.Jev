namespace Jev.Net.Examples;

/// <summary>
/// The pattern that makes this model usable in production: act on confident answers automatically,
/// and send the rest to a human. You choose the threshold; the model reports how sure it is.
/// </summary>
public static class ConfidenceRouting
{
    private const double AutoApproveAbove = 0.85;

    public static async Task RunAsync(IJevClient client)
    {
        string[] comments =
        [
            "This is spam, buy followers at cheap-followers dot biz",
            "I disagree with the author but the data is solid.",
            "lol",
        ];

        foreach (var comment in comments)
        {
            var res = await client.SystemOneAsync(comment, new Dictionary<string, Question>
            {
                ["verdict"] = Question.Choice("Should this comment be published?", new Dictionary<string, JsonContent>
                {
                    ["publish"] = "Normal on-topic comment",
                    ["spam"] = "Advertising, link farming, or bot output",
                    ["abuse"] = "Harassment or hate speech",
                }),
            });

            var verdict = res.Choice("verdict");
            var action = verdict.Confidence >= AutoApproveAbove
                ? $"auto: {verdict.Choice}"
                : $"human review (model leaned {verdict.Choice} at {verdict.Confidence:P0})";

            Console.WriteLine($"{comment,-55} -> {action}");
        }
    }
}
