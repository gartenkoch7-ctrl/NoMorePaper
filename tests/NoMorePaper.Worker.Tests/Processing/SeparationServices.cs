using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Separation;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Processing;

namespace NoMorePaper.Worker.Tests.Processing;

/// <summary>What the separation stage needs besides the runner (registered by <c>AddStageRunners</c>), like the Worker wires it.</summary>
internal static class SeparationServices
{
    public static IServiceCollection AddSeparation(this IServiceCollection services, ISeparationAssessor assessor)
    {
        services.AddSingleton(assessor);
        services.AddScoped<IDocumentPages, EfDocumentPages>();
        services.AddScoped<ISeparationQueue, JobQueue>();

        // Loggers the harness does not record itself, e.g. the separation runner's.
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        return services;
    }
}
