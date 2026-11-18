using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NoMorePaper.Server.Telemetry;

/// <summary>Activity source and meter of the Server project (docs/kb/telemetrie.md).</summary>
internal static class ServerTelemetry
{
    public const string Name = "NoMorePaper.Server";

    public static readonly ActivitySource Source = new(Name);

    public static readonly Meter Meter = new(Name);
}
