namespace TypeSafe.Jev.Examples;

/// <summary>
/// State does not have to be a string. Pass an object or array and it is serialized as JSON,
/// so the model sees labelled fields instead of a paragraph you had to template by hand.
/// </summary>
public static class StructuredState
{
    private sealed record Order(string Id, decimal Total, string Country, int PreviousOrders, bool BillingMatchesShipping, string Email);

    public static async Task RunAsync(IJevClient client)
    {
        var order = new Order(
            Id: "ORD-91821",
            Total: 2340.00m,
            Country: "MT",
            PreviousOrders: 0,
            BillingMatchesShipping: false,
            Email: "qw8812x@mailinator.com");

        var res = await client.SystemOneAsync(JsonContent.From(order), new Dictionary<string, Question>
        {
            ["fraud"] = Question.Noul("Does this order look fraudulent?"),
            ["review"] = Question.Score("How much manual review does it need?", "none, ship it", "quick glance", "full manual review", "hold and contact the customer"),
        });

        Console.WriteLine($"order {order.Id}");
        Console.WriteLine($"  fraud signal:  {res.Noul("fraud").Noul:P0}");
        Console.WriteLine($"  review needed: {res.Score("review").Score:0.0}");
    }
}
