using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NoMorePaper.Infrastructure.Telemetry;

/// <summary>Activity source and meter of the Infrastructure project (docs/kb/telemetrie.md).</summary>
public static class InfrastructureTelemetry
{
    public const string Name = "NoMorePaper.Infrastructure";

    internal static readonly ActivitySource Source = new(Name);

    internal static readonly Meter Meter = new(Name);
}
