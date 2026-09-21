namespace Jev.Net.Examples;

/// <summary>
/// Which models the account can use. Aliases like <c>jev-latest</c> track the newest release;
/// pin a concrete version when you need reproducible answers.
/// </summary>
public static class ListModels
{
    public static async Task RunAsync(IJevClient client)
    {
        var models = await client.ListModelsAsync();

        foreach (var m in models)
            Console.WriteLine($"{m.Name,-16} {m.ReleaseDate,-12} {m.Description}");
    }
}
