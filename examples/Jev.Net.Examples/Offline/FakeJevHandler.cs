using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Jev.Net.Examples;

/// <summary>
/// Answers requests locally so every example runs without an API key or a network.
/// It reads the questions you actually sent and synthesizes a well-formed response for each one,
/// which makes it a real end-to-end check of request encoding and response decoding — just not of the model.
/// Values are derived from the state and the question name, so runs are repeatable.
/// </summary>
internal sealed class FakeJevHandler : HttpMessageHandler
{
    private const string AnsweringModel = "jev-1.13.0-offline";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var json = path.EndsWith("/v1/models", StringComparison.Ordinal)
            ? Models()
            : SystemOne(JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json.ToJsonString(), Encoding.UTF8, "application/json"),
            Headers = { { "x-typesafe-request-id", "offline-" + Math.Abs(json.ToJsonString().GetHashCode()) } },
        };
    }

    private static JsonNode Models() => new JsonArray(
        new JsonObject { ["name"] = "jev-latest", ["description"] = "Alias for the newest stable release", ["release_date"] = "2026-05-01" },
        new JsonObject { ["name"] = "jev-1.13.0", ["description"] = "System One decision model", ["release_date"] = "2026-05-01" });

    private static JsonNode SystemOne(JsonNode request)
    {
        var state = request["state"]?.ToJsonString() ?? "";
        var questions = request["questions"]!.AsObject();
        var answers = new JsonObject();
        var inputTokens = 40 + state.Length / 4;

        foreach (var (name, question) in questions)
        {
            // The whole question feeds the seed, so changing criteria changes the answer.
            var seed = Seed(state + "|" + name + "|" + question!.ToJsonString());
            answers[name] = question!["type"]!.GetValue<string>() switch
            {
                "noul" => Noul(seed),
                "choice" => Choice(question["criteria"]!.AsObject(), seed),
                "score" => Score(question["criteria"]!.AsArray(), seed),
                var t => throw new InvalidOperationException($"The fake handler does not know question type '{t}'."),
            };
            inputTokens += question.ToJsonString().Length / 4;
        }

        return new JsonObject
        {
            ["model"] = AnsweringModel,
            ["answers"] = answers,
            ["usage"] = new JsonObject { ["input_tokens"] = inputTokens, ["output_tokens"] = 8 * questions.Count },
        };
    }

    private static JsonNode Noul(uint seed) =>
        new JsonObject { ["type"] = "noul", ["noul"] = Round(Unit(seed)) };

    private static JsonNode Choice(JsonObject criteria, uint seed)
    {
        var names = criteria.Select(kv => kv.Key).ToArray();
        var winner = (int)(seed % (uint)names.Length);
        var confidence = 0.55 + Unit(seed >> 8) * 0.44;

        var probabilities = new JsonObject();
        var rest = names.Length > 1 ? (1 - confidence) / (names.Length - 1) : 0;
        for (var i = 0; i < names.Length; i++)
            probabilities[names[i]] = Round(i == winner ? confidence : rest);

        return new JsonObject
        {
            ["type"] = "choice",
            ["choice"] = names[winner],
            ["confidence"] = Round(confidence),
            ["probabilities"] = probabilities,
        };
    }

    private static JsonNode Score(JsonArray levels, uint seed)
    {
        var top = (int)(seed % (uint)levels.Count);
        var confidence = 0.55 + Unit(seed >> 8) * 0.44;

        var probabilities = new JsonObject();
        var legend = new JsonObject();
        var rest = levels.Count > 1 ? (1 - confidence) / (levels.Count - 1) : 0;
        double expected = 0;
        for (var i = 0; i < levels.Count; i++)
        {
            var p = i == top ? confidence : rest;
            probabilities[i.ToString()] = Round(p);
            legend[i.ToString()] = levels[i]?.DeepClone();
            expected += i * p;
        }

        return new JsonObject
        {
            ["type"] = "score",
            ["score"] = Round(expected),
            ["confidence"] = Round(confidence),
            ["legend"] = legend,
            ["probabilities"] = probabilities,
        };
    }

    /// <summary>FNV-1a, so the same input always gives the same answer.</summary>
    private static uint Seed(string text)
    {
        var hash = 2166136261u;
        foreach (var c in text) hash = (hash ^ c) * 16777619u;
        return hash;
    }

    private static double Unit(uint seed) => (seed % 1000) / 1000.0;

    private static double Round(double value) => Math.Round(value, 3);
}
