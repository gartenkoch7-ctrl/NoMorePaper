using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Search;

namespace NoMorePaper.Application.Composition;

/// <summary>Registers the use cases in the composition roots; their ports come from Infrastructure and the hosts.</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Use cases of the HTTP API: intake, list, detail, download and search (Server).</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ReceiveDocument>();
        services.AddScoped<ListDocuments>();
        services.AddScoped<GetDocument>();
        services.AddScoped<OpenDocumentFile>();
        services.AddScoped<SearchDocuments>();
        return services;
    }

    /// <summary>
    /// The processing use case (Worker). Needs <see cref="IOcrEngine"/> and <see cref="IWorkingFolder"/>,
    /// which only the Worker provides, hence apart from <see cref="AddApplication"/>.
    /// </summary>
    public static IServiceCollection AddDocumentProcessing(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ProcessNextDocument>();
        return services;
    }
}
