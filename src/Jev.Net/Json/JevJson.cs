using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jev.Net;

/// <summary>Serializer settings matching the Jev wire format.</summary>
public static class JevJson
{
    /// <summary>
    /// Snake-case property names, nulls omitted, and the source-generated resolver so the whole client
    /// works under trimming and Native AOT.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = JevJsonContext.Default,
        };
        options.MakeReadOnly();
        return options;
    }
}
