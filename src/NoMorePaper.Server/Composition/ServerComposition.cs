using NoMorePaper.Infrastructure.Composition;
using NoMorePaper.Server.Health;
using NoMorePaper.Server.Startup;
using NoMorePaper.Server.Telemetry;

namespace NoMorePaper.Server.Composition;

/// <summary>Composition root of the Server: wires Infrastructure, startup, health and telemetry.</summary>
internal static class ServerComposition
{
    public static WebApplicationBuilder AddServer(this WebApplicationBuilder builder)
    {
        builder.Services.AddInfrastructure(builder.Configuration);

        // Registered before the web host starts, so the schema is current before the first request.
        builder.Services.AddHostedService<MigrateOnStartup>();
        builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
        builder.AddServerTelemetry();

        return builder;
    }
}
