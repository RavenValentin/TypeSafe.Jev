using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TypeSafe.Jev;

/// <summary>A typed question sent to Jev. Use the static factory methods, or the records directly.</summary>
public abstract record Question
{
    /// <summary>What to evaluate: text, or JSON structure. Optional for every question type.</summary>
    public JsonContent Instructions { get; init; }

    /// <summary>Writes this question as the API expects it.</summary>
    internal abstract void Write(Utf8JsonWriter writer);

    /// <summary>Yes/no question answered with a probability from 0 to 1.</summary>
    /// <param name="instructions">The question to answer.</param>
    /// <param name="whenTrue">Optional definition of what counts as yes.</param>
    /// <param name="whenFalse">Optional definition of what counts as no.</param>
    public static NoulQuestion Noul(JsonContent instructions, JsonContent whenTrue = default, JsonContent whenFalse = default) =>
        new()
        {
            Instructions = instructions,
            Criteria = whenTrue.HasValue || whenFalse.HasValue ? new NoulCriteria { True = whenTrue, False = whenFalse } : null,
        };

    /// <summary>Single selection among named options; an unset description means "self-explanatory".</summary>
    /// <param name="instructions">The decision to make.</param>
    /// <param name="options">Options keyed by the name the answer will report.</param>
    public static ChoiceQuestion Choice(JsonContent instructions, IReadOnlyDictionary<string, JsonContent> options) =>
        new() { Instructions = instructions, Criteria = options };

    /// <summary>Single selection among options that need no description.</summary>
    public static ChoiceQuestion Choice(JsonContent instructions, params string[] options) =>
        new() { Instructions = instructions, Criteria = options.ToDictionary(o => o, _ => default(JsonContent)) };

    /// <summary>Single selection among the members of <typeparamref name="TEnum"/>.</summary>
    /// <remarks>
    /// Member names travel as snake_case; a <see cref="DescriptionAttribute"/> on a member becomes its rubric.
    /// Read the answer back with <see cref="ChoiceAnswer.As{TEnum}"/>.
    /// </remarks>
    public static ChoiceQuestion Choice<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(
        JsonContent instructions) where TEnum : struct, Enum =>
        new()
        {
            Instructions = instructions,
            Criteria = Enum.GetValues<TEnum>().ToDictionary(
                EnumNames.ToWire,
                v => (JsonContent)typeof(TEnum).GetField(v.ToString())?.GetCustomAttribute<DescriptionAttribute>()?.Description),
        };

    /// <summary>Rating on an ordered scale; the first level is score 0.</summary>
    /// <param name="instructions">What to rate.</param>
    /// <param name="levels">Level descriptions, lowest first. The API accepts 2–10.</param>
    public static ScoreQuestion Score(JsonContent instructions, params string[] levels) =>
        new() { Instructions = instructions, Criteria = levels.Select(l => (JsonContent)l).ToArray() };

    /// <summary>Rating on an ordered scale whose levels carry their own structure.</summary>
    public static ScoreQuestion Score(JsonContent instructions, IReadOnlyList<JsonContent> levels) =>
        new() { Instructions = instructions, Criteria = levels };

    /// <summary>Sends <paramref name="json"/> exactly as given — the escape hatch for question shapes this SDK does not model yet.</summary>
    public static RawQuestion Raw(JsonObject json) => new(json);

    /// <summary>A <see cref="JsonObject"/> is taken as a raw question.</summary>
    public static implicit operator Question(JsonObject json) => Raw(json);
}
