namespace Jev.Net.Examples;

/// <summary>
/// "Is this a complaint?" means different things to different teams. Spelling out what true and
/// false cover moves the decision boundary to where you want it, without rewording the question.
/// </summary>
public static class NoulBoundaries
{
    public static async Task RunAsync(IJevClient client)
    {
        const string message = "The export worked eventually, but it took four tries and I never found out why.";

        // Without criteria, the model uses its own reading of "complaint".
        var loose = await client.SystemOneAsync(message, new Dictionary<string, Question>
        {
            ["complaint"] = Question.Noul("Is this message a complaint?"),
        });

        // With criteria, you decide where the line sits.
        var strict = await client.SystemOneAsync(message, new Dictionary<string, Question>
        {
            ["complaint"] = Question.Noul(
                "Is this message a complaint?",
                whenTrue: "The customer is dissatisfied and expects something to change, even if politely phrased",
                whenFalse: "Neutral reports, questions, or feedback offered without any expectation of a fix"),
        });

        Console.WriteLine($"without criteria: {loose.Noul("complaint").Noul:P0}");
        Console.WriteLine($"with criteria:    {strict.Noul("complaint").Noul:P0}");
    }
}
