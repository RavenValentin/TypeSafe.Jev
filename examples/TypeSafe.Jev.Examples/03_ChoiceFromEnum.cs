using System.ComponentModel;

namespace TypeSafe.Jev.Examples;

/// <summary>
/// The typed way to classify: the options are an enum, the descriptions are [Description] attributes,
/// and the answer comes back as that enum. Add a member and both the request and the switch update together.
/// </summary>
public static class ChoiceFromEnum
{
    /// <summary>Member names travel as snake_case: <c>TechnicalIssue</c> becomes <c>technical_issue</c>.</summary>
    public enum Category
    {
        [Description("Charges, refunds, invoices, subscription changes")]
        Billing,

        [Description("Bugs, outages, broken integrations")]
        TechnicalIssue,

        [Description("Asking for a capability the product does not have yet")]
        FeatureRequest,

        Other,
    }

    public static async Task RunAsync(IJevClient client)
    {
        var res = await client.SystemOneAsync(
            "Could you add Slack notifications when an invoice fails? Right now I only find out days later.",
            new Dictionary<string, Question> { ["category"] = Question.Choice<Category>("What is this ticket about?") });

        Category category = res.Choice("category").As<Category>();

        Console.WriteLine($"category: {category} (confidence {res.Choice("category").Confidence:P0})");
        Console.WriteLine(category switch
        {
            Category.Billing => "-> finance team",
            Category.TechnicalIssue => "-> on-call engineer",
            Category.FeatureRequest => "-> product backlog",
            _ => "-> triage by hand",
        });
    }
}
