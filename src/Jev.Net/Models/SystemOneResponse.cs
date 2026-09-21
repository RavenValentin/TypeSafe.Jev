using System.Text.Json.Serialization;

namespace Jev.Net;

/// <summary>
/// Result of a System One call. Derive from it and declare answer-typed properties to have them filled
/// by name — see <see cref="IJevClient.SystemOneAsync{T}"/>.
/// </summary>
public record SystemOneResponse
{
    /// <summary>Model that produced the answers, e.g. <c>jev-1.13.0</c>.</summary>
    public string Model { get; init; } = "";

    /// <summary>Answers keyed by question name.</summary>
    public IReadOnlyDictionary<string, Answer> Answers { get; init; } = new Dictionary<string, Answer>();

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
        Answers.TryGetValue(name, out var answer)
            ? answer as T ?? throw new JevResponseValidationException(
                $"Answer '{name}' is {answer.GetType().Name}, not {typeof(T).Name}.", $"answers.{name}")
            : throw new JevResponseValidationException($"No answer named '{name}'.", $"answers.{name}");
}

/// <summary>
/// Marks an answer property on a <see cref="SystemOneResponse"/> subclass as optional: a response without it
/// is accepted and the property left null. Without this, a missing answer is a validation error.
/// </summary>
/// <remarks>
/// Optional is marked rather than inferred from nullability, because the trimmer removes nullability metadata —
/// inferring from it would make the same class validate differently in a Native AOT build.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class OptionalAnswerAttribute : Attribute;
