using System.Text.Json.Nodes;

namespace TypeSafe.Jev.Examples;

/// <summary>
/// Instructions, choice options and score levels all accept JSON, not just text. Use it when a rubric
/// needs its own structure — what a level means, what signals to look for, what it is not.
/// Build the question records directly instead of using the string factories.
/// </summary>
public static class StructuredInstructions
{
    public static async Task RunAsync(IJevClient client)
    {
        var tier = new ChoiceQuestion
        {
            Instructions = new JsonObject
            {
                ["field"] = new JsonObject
                {
                    ["name"] = "support_tier",
                    ["type"] = "string",
                    ["description"] = "Which support tier should own this conversation",
                },
                ["question"] = "Route the ticket to the right tier.",
            },
            Criteria = new Dictionary<string, JsonContent>
            {
                ["self_serve"] = new JsonObject
                {
                    ["what"] = "Answered by an existing help article",
                    ["not_for"] = "Anything touching the customer's data or money",
                    ["examples"] = new JsonArray("how do I change my password", "where are my invoices"),
                },
                ["tier_one"] = new JsonObject
                {
                    ["what"] = "A human can resolve it without engineering",
                    ["examples"] = new JsonArray("refund request", "seat added to the wrong workspace"),
                },
                ["engineering"] = new JsonObject
                {
                    ["what"] = "Needs code, logs, or a deploy",
                    ["examples"] = new JsonArray("webhooks stopped firing", "500 on checkout"),
                },
            },
        };

        var effort = new ScoreQuestion
        {
            Instructions = "How much engineering effort would resolving this take?",
            Criteria =
            [
                new JsonObject { ["summary"] = "minutes", ["signals"] = new JsonArray("config change", "toggle a flag") },
                new JsonObject { ["summary"] = "hours", ["signals"] = new JsonArray("a focused bug fix", "one service touched") },
                new JsonObject { ["summary"] = "days", ["signals"] = new JsonArray("several services", "a migration", "unclear root cause") },
            ],
        };

        var res = await client.SystemOneAsync(
            "Webhook deliveries stopped after I rotated the signing secret; your docs say rotation is zero-downtime.",
            new Dictionary<string, Question> { ["tier"] = tier, ["effort"] = effort });

        Console.WriteLine($"tier:   {res.Choice("tier").Choice} (confidence {res.Choice("tier").Confidence:P0})");
        Console.WriteLine($"effort: {res.Score("effort").Score:0.0}");
    }
}
