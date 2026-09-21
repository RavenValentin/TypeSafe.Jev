using System.Text.Json;
using System.Text.Json.Nodes;

namespace TypeSafe.Jev;

/// <summary>
/// A question sent exactly as written — the escape hatch for fields or question types the API gains
/// before this SDK models them. Only its structure is checked; its schema is left to the API.
/// </summary>
public sealed record RawQuestion : Question
{
    private readonly JsonObject _json;

    /// <summary>Wraps <paramref name="json"/>, cloning it so it can be reused and resent.</summary>
    /// <exception cref="ArgumentException">
    /// No <c>type</c>; or a <c>choice</c> or <c>score</c> without <c>criteria</c>; or an empty score rubric.
    /// </exception>
    public RawQuestion(JsonObject json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var type = json["type"]?.GetValueKind() == JsonValueKind.String ? json["type"]!.GetValue<string>() : null;
        if (type is null) throw new ArgumentException("A raw question needs a string 'type'.", nameof(json));

        switch (type)
        {
            case "choice" when json["criteria"] is not JsonObject:
                throw new ArgumentException("A raw choice question needs an object 'criteria'.", nameof(json));
            case "score" when json["criteria"] is not JsonArray:
                throw new ArgumentException("A raw score question needs an array 'criteria'.", nameof(json));
            case "score" when json["criteria"] is JsonArray { Count: 0 }:
                throw new ArgumentException("A raw score question needs at least one level.", nameof(json));
        }

        _json = (JsonObject)json.DeepClone();
    }

    /// <summary>A private copy of the JSON that will be sent.</summary>
    public JsonObject Json => (JsonObject)_json.DeepClone();

    internal override void Write(Utf8JsonWriter writer) => _json.WriteTo(writer);
}
