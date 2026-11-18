using Microsoft.AspNetCore.Http.Features;
using NoMorePaper.Application.Composition;
using NoMorePaper.Application.Telemetry;
using NoMorePaper.Infrastructure.Composition;
using NoMorePaper.Infrastructure.Telemetry;
using NoMorePaper.Server.Documents;
using NoMorePaper.Server.Health;
using NoMorePaper.Server.Hosting;
using NoMorePaper.Server.Startup;
using NoMorePaper.Server.Telemetry;

namespace NoMorePaper.Server.Composition;

/// <summary>
/// Composition root of the Server: wires Application, Infrastructure, startup, health and telemetry.
/// The only place in the Server that knows Infrastructure types; endpoints call use cases.
/// </summary>
internal static class ServerComposition
{
    public static WebApplicationBuilder AddServer(this WebApplicationBuilder builder)
    {
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddApplication();
        builder.Services.AddProblemDetails();
        builder.Services.AddServerForwardedHeaders();

        // Stated here so the 100 MB upload limit does not silently depend on a framework default.
        builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = DocumentUpload.MaxRequestBytes);
        builder.Services.AddScoped<DocumentUpload>();

        // Registered before the web host starts, so the schema is current before the first request.
        builder.Services.AddHostedService<MigrateOnStartup>();
        builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
        builder.AddServerTelemetry(ApplicationTelemetry.Name, InfrastructureTelemetry.Name);

        return builder;
    }
}
