using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Jev.Net;

/// <summary>
/// The names to subscribe to for traces and metrics. Both ship in the .NET runtime, so OpenTelemetry
/// support costs no package reference — and with nobody listening the client skips the bookkeeping entirely.
/// </summary>
/// <example>
/// <code>
/// services.AddOpenTelemetry()
///     .WithTracing(t => t.AddSource(JevTelemetry.ActivitySourceName))
///     .WithMetrics(m => m.AddMeter(JevTelemetry.MeterName));
/// </code>
/// </example>
public static class JevTelemetry
{
    /// <summary>Name of the <see cref="System.Diagnostics.ActivitySource"/> the client writes spans to.</summary>
    public const string ActivitySourceName = "Jev.Net";

    /// <summary>Name of the <see cref="System.Diagnostics.Metrics.Meter"/> the client writes instruments to.</summary>
    public const string MeterName = "Jev.Net";

    /// <summary>Histogram, seconds: one whole call including its retries and waits.</summary>
    public const string DurationMetricName = "jev_net.client.request.duration";

    /// <summary>Counter: attempts after the first.</summary>
    public const string RetriesMetricName = "jev_net.client.retries";

    /// <summary>Counter: tokens reported by the API, tagged input or output.</summary>
    public const string TokensMetricName = "jev_net.client.token.usage";

    internal static ActivitySource Source { get; } = new(ActivitySourceName, JevDefaults.SdkVersion);

    private static readonly Meter Meter = new(MeterName, JevDefaults.SdkVersion);

    internal static Histogram<double> Duration { get; } = Meter.CreateHistogram<double>(
        DurationMetricName, unit: "s", description: "Duration of one Jev API call, retries and waits included.");

    internal static Counter<long> Retries { get; } = Meter.CreateCounter<long>(
        RetriesMetricName, unit: "{attempt}", description: "Attempts after the first.");

    internal static Counter<long> Tokens { get; } = Meter.CreateCounter<long>(
        TokensMetricName, unit: "{token}", description: "Tokens reported by the API.");
}
