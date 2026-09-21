namespace TypeSafe.Jev.Examples;

/// <summary>
/// A flat choice over 200 leaves asks the model to hold the whole taxonomy at once.
/// Walking the tree one level at a time keeps each decision small — and you can stop early
/// when confidence drops instead of committing to a bad leaf.
/// </summary>
public static class TaxonomyWalk
{
    private static readonly Dictionary<string, Dictionary<string, JsonContent>> Taxonomy = new()
    {
        ["root"] = new()
        {
            ["hardware"] = "Physical devices: laptops, phones, peripherals",
            ["software"] = "Applications, operating systems, services",
            ["billing"] = "Money: invoices, refunds, subscriptions",
        },
        ["software"] = new()
        {
            ["os"] = "Operating system itself",
            ["application"] = "A specific app the user runs",
            ["network"] = "Connectivity, DNS, VPN, proxies",
        },
        ["network"] = new()
        {
            ["vpn"] = "Corporate VPN access",
            ["dns"] = "Name resolution failures",
            ["wifi"] = "Wireless connectivity",
        },
    };

    public static async Task RunAsync(IJevClient client)
    {
        const string ticket = "Since yesterday I can't resolve any internal hostnames when I'm on the corporate VPN.";
        const double minConfidence = 0.6;

        var node = "root";
        var path = new List<string>();

        while (Taxonomy.TryGetValue(node, out var children))
        {
            var res = await client.SystemOneAsync(ticket, new Dictionary<string, Question>
            {
                ["level"] = Question.Choice($"Which category fits best, given we are under '{node}'?", children),
            });

            var answer = res.Choice("level");
            Console.WriteLine($"{node,-10} -> {answer.Choice,-12} ({answer.Confidence:P0})");

            if (answer.Confidence < minConfidence)
            {
                Console.WriteLine($"   confidence below {minConfidence:P0}, stopping here for a human to finish");
                break;
            }

            path.Add(answer.Choice);
            node = answer.Choice;
        }

        Console.WriteLine($"\npath: {string.Join(" / ", path)}");
    }
}
