using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Jev.Net;

/// <summary>
/// Anywhere the API accepts "text, an object, or an array" — state, instructions, criteria — it accepts a
/// <see cref="JsonContent"/>. It converts implicitly from <see cref="string"/> and <see cref="JsonNode"/>,
/// and <see cref="From{T}(T, JsonTypeInfo{T})"/> takes anything else.
/// </summary>
/// <remarks>
/// Nodes are deep-cloned on the way in and on the way out, so one <see cref="JsonObject"/> can appear in
/// several questions and a question can be sent any number of times. <c>System.Text.Json</c> nodes have a
/// single parent, so holding one by reference would throw the second time it was used.
/// <para>
/// A default <see cref="JsonContent"/> means <em>omit this field</em>; <see cref="Null"/> means
/// <em>send an explicit <c>null</c></em>.
/// </para>
/// </remarks>
public readonly struct JsonContent : IEquatable<JsonContent>
{
    private readonly JsonNode? _node;
    private readonly bool _present;

    private JsonContent(JsonNode? node, bool present)
    {
        _node = node;
        _present = present;
    }

    /// <summary>An explicit JSON <c>null</c>, as opposed to an omitted field.</summary>
    public static JsonContent Null => new(null, present: true);

    /// <summary><c>true</c> when this value should be written at all.</summary>
    public bool HasValue => _present;

    /// <summary>Wraps a node, cloning it so the caller keeps ownership of theirs.</summary>
    public static JsonContent FromNode(JsonNode? node) => new(node?.DeepClone(), present: true);

    /// <summary>Serializes <paramref name="value"/> with <paramref name="typeInfo"/>; safe under trimming and Native AOT.</summary>
    public static JsonContent From<T>(T value, JsonTypeInfo<T> typeInfo) =>
        new(JsonSerializer.SerializeToNode(value, typeInfo), present: true);

    /// <summary>Serializes <paramref name="value"/> by reflection. Prefer the <see cref="JsonTypeInfo{T}"/> overload in trimmed or AOT apps.</summary>
    /// <remarks>
    /// This deliberately does not use <see cref="JevJson.Options"/>: those carry the source-generated resolver,
    /// which knows this SDK's own types and nothing else. Reflection is the point of this overload.
    /// </remarks>
    [RequiresUnreferencedCode("Serializes an arbitrary type by reflection. Pass a JsonTypeInfo<T> in trimmed or AOT apps.")]
    [RequiresDynamicCode("Serializes an arbitrary type by reflection. Pass a JsonTypeInfo<T> in trimmed or AOT apps.")]
    public static JsonContent From<T>(T value, JsonSerializerOptions? options = null) =>
        new(JsonSerializer.SerializeToNode(value, options ?? ReflectionOptions.Value), present: true);

    // Left mutable on purpose: the serializer fills in the default reflection resolver on first use, which is
    // exactly the behaviour this overload is annotated for. Marking it read-only here would throw instead.
    private static readonly Lazy<JsonSerializerOptions> ReflectionOptions = new(static () => new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    });

    /// <summary>A private copy of the node, or <c>null</c> for an explicit or absent null.</summary>
    public JsonNode? ToNode() => _node?.DeepClone();

    /// <summary>Writes the value, or nothing when it was never set.</summary>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        if (!_present) return;
        if (_node is null) writer.WriteNullValue();
        else _node.WriteTo(writer);
    }

    /// <summary>Writes <paramref name="name"/> and the value, or nothing when it was never set.</summary>
    internal void WriteTo(Utf8JsonWriter writer, string name)
    {
        if (!_present) return;
        writer.WritePropertyName(name);
        if (_node is null) writer.WriteNullValue();
        else _node.WriteTo(writer);
    }

    /// <summary>Text becomes a JSON string; <c>null</c> becomes an explicit null.</summary>
    public static implicit operator JsonContent(string? text) => new(text is null ? null : JsonValue.Create(text), present: true);

    /// <summary>A node is cloned in, so the caller can keep using theirs.</summary>
    public static implicit operator JsonContent(JsonNode? node) => FromNode(node);

    /// <inheritdoc />
    public bool Equals(JsonContent other) =>
        _present == other._present && JsonNode.DeepEquals(_node, other._node);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is JsonContent other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_present, _node?.ToJsonString());

    /// <inheritdoc />
    public override string ToString() => !_present ? "<unset>" : _node?.ToJsonString() ?? "null";

    /// <summary>Compares two values.</summary>
    public static bool operator ==(JsonContent left, JsonContent right) => left.Equals(right);

    /// <summary>Compares two values.</summary>
    public static bool operator !=(JsonContent left, JsonContent right) => !left.Equals(right);
}
