using System.Text.Json.Serialization;

namespace TypeSafe.Jev;

/// <summary>
/// Source-generated metadata for every type that crosses the wire. Having it means the client never
/// reflects over its own models, which is what makes the package trim- and Native AOT-safe.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SystemOneResponse))]
[JsonSerializable(typeof(Answer))]
[JsonSerializable(typeof(NoulAnswer))]
[JsonSerializable(typeof(ChoiceAnswer))]
[JsonSerializable(typeof(ScoreAnswer))]
[JsonSerializable(typeof(Usage))]
[JsonSerializable(typeof(ModelCard))]
[JsonSerializable(typeof(List<ModelCard>))]
[JsonSerializable(typeof(ModelList))]
internal sealed partial class JevJsonContext : JsonSerializerContext;
