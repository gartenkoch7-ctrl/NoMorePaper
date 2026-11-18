using NoMorePaper.Infrastructure.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NoMorePaper.Server.Telemetry;

/// <summary>OpenTelemetry for the Server; exports over OTLP only when a collector is configured.</summary>
internal static class ServerTelemetryRegistration
{
    private const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    public static void AddServerTelemetry(this WebApplicationBuilder builder)
    {
        // No endpoint, no exporter: by default nothing leaves the host (AGENTS.md 2.4).
        if (string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointKey]))
        {
            return;
        }

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("nomorepaper-server"))
            .WithTracing(tracing => tracing
                .AddSource(ServerTelemetry.Name, InfrastructureTelemetry.Name)
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health", StringComparison.Ordinal)))
            .WithMetrics(metrics => metrics
                .AddMeter(ServerTelemetry.Name, InfrastructureTelemetry.Name)
                .AddAspNetCoreInstrumentation())
            .WithLogging()
            .UseOtlpExporter();
    }
}
