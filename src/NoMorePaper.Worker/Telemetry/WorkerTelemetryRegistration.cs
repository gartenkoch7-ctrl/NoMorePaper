using Microsoft.AspNetCore.Builder;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NoMorePaper.Worker.Telemetry;

/// <summary>OpenTelemetry for the Worker; exports over OTLP only when a collector is configured.</summary>
internal static class WorkerTelemetryRegistration
{
    private const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <param name="libraryNames">Activity sources and meters of the libraries the Worker runs, named by the composition root.</param>
    public static void AddWorkerTelemetry(this WebApplicationBuilder builder, params string[] libraryNames)
    {
        // No endpoint, no exporter: by default nothing leaves the host (AGENTS.md 2.4).
        if (string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointKey]))
        {
            return;
        }

        string[] names = [WorkerTelemetry.Name, .. libraryNames];
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("nomorepaper-worker"))
            .WithTracing(tracing => tracing.AddSource(names))
            .WithMetrics(metrics => metrics.AddMeter(names))
            .WithLogging()
            .UseOtlpExporter();
    }
}
