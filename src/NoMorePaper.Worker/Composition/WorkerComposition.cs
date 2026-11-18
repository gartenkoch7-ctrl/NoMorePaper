using Microsoft.AspNetCore.Builder;
using NoMorePaper.Infrastructure.Composition;
using NoMorePaper.Worker.Heartbeat;
using NoMorePaper.Worker.Telemetry;

namespace NoMorePaper.Worker.Composition;

/// <summary>Composition root of the Worker (ADR-0006): background jobs plus a health endpoint.</summary>
internal static class WorkerComposition
{
    public static WebApplicationBuilder AddWorker(this WebApplicationBuilder builder)
    {
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddHostedService<HeartbeatWriter>();
        builder.Services.AddHealthChecks().AddCheck<HeartbeatHealthCheck>("heartbeat");
        builder.AddWorkerTelemetry();

        return builder;
    }
}
