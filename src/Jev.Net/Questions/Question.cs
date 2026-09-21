using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Jev.Net;

/// <summary>A typed question sent to Jev. Use the static factory methods to build one.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(NoulQuestion), "noul")]
[JsonDerivedType(typeof(ChoiceQuestion), "choice")]
[JsonDerivedType(typeof(ScoreQuestion), "score")]
public abstract record Question
{
    /// <summary>What to evaluate: text or JSON structure.</summary>
    public required JsonNode? Instructions { get; init; }

    /// <summary>Yes/no question answered with a probability from 0 to 1.</summary>
    /// <param name="instructions">The question to answer.</param>
    /// <param name="whenTrue">Optional definition of what counts as yes.</param>
    /// <param name="whenFalse">Optional definition of what counts as no.</param>
    public static NoulQuestion Noul(string instructions, string? whenTrue = null, string? whenFalse = null) =>
        new()
        {
            Instructions = instructions,
            Criteria = whenTrue is null && whenFalse is null ? null : new NoulCriteria { True = whenTrue, False = whenFalse },
        };

    /// <summary>Single selection among named options; a <c>null</c> description means "self-explanatory".</summary>
    /// <param name="instructions">The decision to make.</param>
    /// <param name="options">1–255 options keyed by the name the answer will report.</param>
    public static ChoiceQuestion Choice(string instructions, IReadOnlyDictionary<string, string?> options) =>
        new()
        {
            Instructions = instructions,
            Criteria = options.ToDictionary(kv => kv.Key, kv => (JsonNode?)kv.Value),
        };

    /// <summary>Single selection among the members of <typeparamref name="TEnum"/>.</summary>
    /// <remarks>
    /// Member names are sent as snake_case; a <see cref="DescriptionAttribute"/> on a member becomes its rubric.
    /// Read the answer back with <see cref="ChoiceAnswer.As{TEnum}"/>.
    /// </remarks>
    public static ChoiceQuestion Choice<TEnum>(string instructions) where TEnum : struct, Enum =>
        new()
        {
            Instructions = instructions,
            Criteria = Enum.GetValues<TEnum>().ToDictionary(
                v => EnumNames.ToWire(v),
                v => (JsonNode?)typeof(TEnum).GetField(v.ToString())?.GetCustomAttribute<DescriptionAttribute>()?.Description),
        };

    /// <summary>Rating on an ordered scale; the first level is score 0.</summary>
    /// <param name="instructions">What to rate.</param>
    /// <param name="levels">2–10 level descriptions, from lowest to highest.</param>
    public static ScoreQuestion Score(string instructions, params string[] levels) =>
        new() { Instructions = instructions, Criteria = levels.Select(l => (JsonNode?)l).ToArray() };
}
