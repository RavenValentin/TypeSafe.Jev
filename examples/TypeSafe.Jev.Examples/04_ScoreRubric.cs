namespace TypeSafe.Jev.Examples;

/// <summary>
/// A score rates against an ordered rubric of 2–10 levels. The answer is the probability-weighted
/// average, so 1.7 means "mostly level 2, with some pull towards level 1" — finer than any single level.
/// </summary>
public static class ScoreRubric
{
    public static async Task RunAsync(IJevClient client)
    {
        var res = await client.SystemOneAsync(
            "This is the third time I'm writing about the same bug. Nobody has replied. Fix it or refund me.",
            new Dictionary<string, Question>
            {
                ["frustration"] = Question.Score(
                    "How frustrated is this customer?",
                    "calm and matter-of-fact",
                    "mildly annoyed",
                    "clearly frustrated",
                    "angry, threatening to leave"),
            });

        var score = res.Score("frustration");
        Console.WriteLine($"frustration: {score.Score:0.00} of {score.Legend.Count - 1} (confidence {score.Confidence:P0})");

        foreach (var (level, p) in score.Probabilities.OrderBy(p => p.Key))
            Console.WriteLine($"  {level}  {score.Legend[level],-30} {p:P1}");
    }
}
