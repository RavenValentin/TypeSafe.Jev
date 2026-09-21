using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
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
    public static NoulQuestion Noul(string instructions, string? whenTrue = null, string? whenFalse = null) =>
        new()
        {
            Instructions = instructions,
            Criteria = whenTrue is null && whenFalse is null ? null : new NoulCriteria { True = whenTrue, False = whenFalse },
        };

    /// <summary>Single selection among named options; a <c>null</c> description means "self-explanatory".</summary>
    public static ChoiceQuestion Choice(string instructions, IReadOnlyDictionary<string, string?> options) =>
        new()
        {
            Instructions = instructions,
            Criteria = options.ToDictionary(kv => kv.Key, kv => (JsonNode?)kv.Value),
        };

    /// <summary>Single selection among the members of <typeparamref name="TEnum"/>; option descriptions come from <see cref="DescriptionAttribute"/>.</summary>
    public static ChoiceQuestion Choice<TEnum>(string instructions) where TEnum : struct, Enum =>
        new()
        {
            Instructions = instructions,
            Criteria = Enum.GetValues<TEnum>().ToDictionary(
                v => EnumNames.ToWire(v),
                v => (JsonNode?)typeof(TEnum).GetField(v.ToString())?.GetCustomAttribute<DescriptionAttribute>()?.Description),
        };

    /// <summary>Rating on an ordered scale of 2–10 levels; the first level is score 0.</summary>
    public static ScoreQuestion Score(string instructions, params string[] levels) =>
        new() { Instructions = instructions, Criteria = levels.Select(l => (JsonNode?)l).ToArray() };
}

/// <summary>Yes/no question.</summary>
public sealed record NoulQuestion : Question
{
    /// <summary>Optional definitions of what "true" and "false" mean.</summary>
    public NoulCriteria? Criteria { get; init; }
}

/// <summary>Boundaries for a <see cref="NoulQuestion"/>.</summary>
public sealed record NoulCriteria
{
    /// <summary>What counts as yes/true.</summary>
    public JsonNode? True { get; init; }
    /// <summary>What counts as no/false.</summary>
    public JsonNode? False { get; init; }
}

/// <summary>Single-selection question.</summary>
public sealed record ChoiceQuestion : Question
{
    /// <summary>Options keyed by name (1–255), each with an optional description.</summary>
    public required IReadOnlyDictionary<string, JsonNode?> Criteria { get; init; }
}

/// <summary>Ordered-scale question.</summary>
public sealed record ScoreQuestion : Question
{
    /// <summary>Level descriptions (2–10), index = score.</summary>
    public required IReadOnlyList<JsonNode?> Criteria { get; init; }
}

/// <summary>Maps enum members to wire names (snake_case of the member name) and back.</summary>
public static class EnumNames
{
    /// <summary>Wire name of an enum member.</summary>
    public static string ToWire<TEnum>(TEnum value) where TEnum : struct, Enum =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    /// <summary>Parses a wire name back to the enum member; <c>null</c> when unknown.</summary>
    public static TEnum? FromWire<TEnum>(string name) where TEnum : struct, Enum
    {
        foreach (var v in Enum.GetValues<TEnum>())
            if (ToWire(v) == name) return v;
        return null;
    }
}
