using Microsoft.AspNetCore.Builder;
using NoMorePaper.Infrastructure.Telemetry;
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

    public static void AddWorkerTelemetry(this WebApplicationBuilder builder)
    {
        // No endpoint, no exporter: by default nothing leaves the host (AGENTS.md 2.4).
        if (string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointKey]))
        {
            return;
        }

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("nomorepaper-worker"))
            .WithTracing(tracing => tracing.AddSource(WorkerTelemetry.Name, InfrastructureTelemetry.Name))
            .WithMetrics(metrics => metrics.AddMeter(WorkerTelemetry.Name, InfrastructureTelemetry.Name))
            .WithLogging()
            .UseOtlpExporter();
    }
}
