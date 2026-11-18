using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NoMorePaper.Application.Telemetry;

/// <summary>Activity source and meter of the Application project (docs/kb/telemetrie.md).</summary>
public static class ApplicationTelemetry
{
    public const string Name = "NoMorePaper.Application";

    internal static readonly ActivitySource Source = new(Name);

    internal static readonly Meter Meter = new(Name);
}
