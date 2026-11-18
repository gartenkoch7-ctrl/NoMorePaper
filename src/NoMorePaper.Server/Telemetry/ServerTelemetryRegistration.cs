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

    /// <param name="libraryNames">Activity sources and meters of the libraries the Server runs, named by the composition root.</param>
    public static void AddServerTelemetry(this WebApplicationBuilder builder, params string[] libraryNames)
    {
        // No endpoint, no exporter: by default nothing leaves the host (AGENTS.md 2.4).
        if (string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointKey]))
        {
            return;
        }

        string[] names = [ServerTelemetry.Name, .. libraryNames];
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("nomorepaper-server"))
            .WithTracing(tracing => tracing
                .AddSource(names)
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health", StringComparison.Ordinal)))
            .WithMetrics(metrics => metrics
                .AddMeter(names)
                .AddAspNetCoreInstrumentation())
            .WithLogging()
            .UseOtlpExporter();
    }
}
