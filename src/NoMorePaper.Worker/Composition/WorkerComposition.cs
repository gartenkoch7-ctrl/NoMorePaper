using Microsoft.AspNetCore.Builder;
using NoMorePaper.Application.Composition;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Telemetry;
using NoMorePaper.Infrastructure.Composition;
using NoMorePaper.Infrastructure.Ocr;
using NoMorePaper.Infrastructure.Telemetry;
using NoMorePaper.Worker.Heartbeat;
using NoMorePaper.Worker.Processing;
using NoMorePaper.Worker.Telemetry;

namespace NoMorePaper.Worker.Composition;

/// <summary>
/// Composition root of the Worker (ADR-0006): background jobs plus a health endpoint. The only place
/// in the Worker that knows Infrastructure types; everything else uses Application ports and use cases.
/// </summary>
internal static class WorkerComposition
{
    public static WebApplicationBuilder AddWorker(this WebApplicationBuilder builder)
    {
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddDocumentProcessing();
        builder.Services.AddHostedService<HeartbeatWriter>();
        builder.Services.AddHealthChecks().AddCheck<HeartbeatHealthCheck>("heartbeat");
        builder.AddWorkerTelemetry(ApplicationTelemetry.Name, InfrastructureTelemetry.Name);

        // OCR runs only in the Worker, whose image contains the tools (Dockerfile.worker).
        builder.Services.AddSingleton<ExternalProcess>();
        builder.Services.AddSingleton<IOcrEngine, OcrMyPdfEngine>();

        // Read when first resolved, like the data root of the storage (AddInfrastructure).
        var configuration = builder.Configuration;
        builder.Services.AddSingleton(provider => new WorkingFolder(
            Path.Combine(DataPath(configuration), WorkingFolder.FolderName),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<WorkingFolder>>()));
        builder.Services.AddSingleton<IWorkingFolder>(provider => provider.GetRequiredService<WorkingFolder>());
        builder.Services.AddHostedService<DocumentProcessor>();

        return builder;
    }

    private static string DataPath(ConfigurationManager configuration) =>
        configuration[InfrastructureSettings.DataPathKey] is { } path && !string.IsNullOrWhiteSpace(path)
            ? path
            : InfrastructureSettings.DefaultDataPath;
}
