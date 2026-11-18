using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NoMorePaper.Worker.Telemetry;

/// <summary>Activity source, meter and instruments of the Worker project (docs/kb/telemetrie.md).</summary>
internal static class WorkerTelemetry
{
    public const string Name = "NoMorePaper.Worker";

    public static readonly ActivitySource Source = new(Name);

    public static readonly Meter Meter = new(Name);

    public static readonly Counter<long> HeartbeatFailures =
        Meter.CreateCounter<long>("nomorepaper.worker.heartbeat_failures", description: "Heartbeats that could not be written");
}
