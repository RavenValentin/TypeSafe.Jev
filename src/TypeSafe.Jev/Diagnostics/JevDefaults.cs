using System.Reflection;

namespace TypeSafe.Jev;

/// <summary>Constants describing this build of the SDK.</summary>
public static class JevDefaults
{
    /// <summary>Package version, as it appears in the <c>User-Agent</c>.</summary>
    public static string SdkVersion { get; } =
        typeof(JevDefaults).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            is { Length: > 0 } informational
            ? informational.Split('+')[0]
            : typeof(JevDefaults).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>How this client identifies itself. Never claims to be the official SDK.</summary>
    public static string UserAgent { get; } = $"typesafe-jev/{SdkVersion}";

    /// <summary>The API root used when nothing else is configured.</summary>
    public const string BaseAddress = "https://api.typesafe.ai";

    /// <summary>The model alias used when a call does not name one.</summary>
    public const string DefaultModel = "jev-latest";

    /// <summary>Headers the client owns; neither default nor per-call headers may replace them.</summary>
    public static IReadOnlySet<string> ProtectedHeaders { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Accept", "Content-Type", "User-Agent", "X-TypeSafe-SDK", "X-TypeSafe-Runtime",
    };
}
