using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Heartbeat;
using NoMorePaper.Application.Persistence;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Search;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Heartbeat;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Processing;
using NoMorePaper.Infrastructure.Search;

namespace NoMorePaper.Infrastructure.Composition;

/// <summary>Registers the Infrastructure implementations of the Application ports in the composition roots of Server and Worker.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <remarks>
    /// Configuration is read when the services are first resolved, not here, so hosts and test
    /// factories that add configuration sources after this call are still honoured.
    /// </remarks>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(RequireConnectionString(configuration)));
        services.AddScoped<ISchemaMigrator, DatabaseMigrator>();
        services.AddScoped<IDatabaseProbe, DatabaseProbe>();
        services.AddScoped<IHeartbeatStore, HeartbeatStore>();
        services.AddScoped<IJobQueue, JobQueue>();
        services.AddSingleton<IDirectorySync>(OperatingSystem.IsLinux() ? new LibcDirectorySync() : new NoDirectorySync());
        services.AddSingleton<IDocumentStorage>(provider => new FileSystemDocumentStorage(
            DataPath(configuration),
            provider.GetRequiredService<IDirectorySync>(),
            provider.GetRequiredService<ILogger<FileSystemDocumentStorage>>()));
        services.AddScoped<IDocumentRepository, EfDocumentRepository>();
        services.AddScoped<ISearchIndex, PostgresSearchIndex>();

        services.AddNoMorePaperDataProtection(configuration);

        return services;
    }

    private static string DataPath(IConfiguration configuration) =>
        configuration[InfrastructureSettings.DataPathKey] is { } path && !string.IsNullOrWhiteSpace(path)
            ? path
            : InfrastructureSettings.DefaultDataPath;

    private static string RequireConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString(InfrastructureSettings.ConnectionStringName) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"Connection string '{InfrastructureSettings.ConnectionStringName}' is missing (env ConnectionStrings__{InfrastructureSettings.ConnectionStringName}).");
}
