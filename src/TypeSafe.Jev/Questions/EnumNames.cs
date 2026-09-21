using System.Text.Json;

namespace TypeSafe.Jev;

/// <summary>Maps enum members to Jev option names (snake_case of the member name) and back.</summary>
public static class EnumNames
{
    /// <summary>Wire name of an enum member, e.g. <c>TechnicalIssue</c> becomes <c>technical_issue</c>.</summary>
    public static string ToWire<TEnum>(TEnum value) where TEnum : struct, Enum =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()!);

    /// <summary>Parses a wire name back to the enum member; <c>null</c> when it matches no member.</summary>
    public static TEnum? FromWire<TEnum>(string name) where TEnum : struct, Enum
    {
        foreach (var v in Enum.GetValues<TEnum>())
            if (ToWire(v) == name) return v;
        return null;
    }
}
