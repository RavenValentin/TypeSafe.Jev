using System.Text.Json.Serialization;

namespace Jev.Net;

/// <summary>Result of <see cref="IJevClient.SystemOneAsync"/>.</summary>
public sealed record SystemOneResponse
{
    /// <summary>Model that produced the answers, e.g. <c>jev-1.13.0</c>.</summary>
    public required string Model { get; init; }

    /// <summary>Answers keyed by question name.</summary>
    public required IReadOnlyDictionary<string, Answer> Answers { get; init; }

    /// <summary>Token usage, when reported by the API.</summary>
    public Usage? Usage { get; init; }

    /// <summary>Value of the <c>x-typesafe-request-id</c> response header, for support requests.</summary>
    [JsonIgnore] public string? RequestId { get; init; }

    /// <summary>Answer to the noul question <paramref name="name"/>.</summary>
    /// <exception cref="JevResponseValidationException">No such answer, or it is not a noul.</exception>
    public NoulAnswer Noul(string name) => Get<NoulAnswer>(name);

    /// <summary>Answer to the choice question <paramref name="name"/>.</summary>
    /// <exception cref="JevResponseValidationException">No such answer, or it is not a choice.</exception>
    public ChoiceAnswer Choice(string name) => Get<ChoiceAnswer>(name);

    /// <summary>Answer to the score question <paramref name="name"/>.</summary>
    /// <exception cref="JevResponseValidationException">No such answer, or it is not a score.</exception>
    public ScoreAnswer Score(string name) => Get<ScoreAnswer>(name);

    private T Get<T>(string name) where T : Answer =>
        Answers.TryGetValue(name, out var a)
            ? a as T ?? throw new JevResponseValidationException($"Answer '{name}' is {a.GetType().Name}, not {typeof(T).Name}.", $"answers.{name}")
            : throw new JevResponseValidationException($"No answer named '{name}'.", $"answers.{name}");
}
