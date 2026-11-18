using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NoMorePaper.Infrastructure.Heartbeat;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Composition;

/// <summary>Registers the Infrastructure services in the composition roots of Server and Worker.</summary>
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
        services.AddScoped<DatabaseMigrator>();
        services.AddScoped<HeartbeatStore>();

        services.AddDataProtection().SetApplicationName(InfrastructureSettings.DataProtectionApplicationName);
        services.AddOptions<KeyManagementOptions>().Configure<ILoggerFactory>((options, loggerFactory) =>
        {
            // Without a path the framework default applies (user profile on a dev box).
            var path = configuration[InfrastructureSettings.DataProtectionPathKey];
            if (!string.IsNullOrWhiteSpace(path))
            {
                options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(path), loggerFactory);
            }
        });

        return services;
    }

    private static string RequireConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString(InfrastructureSettings.ConnectionStringName) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"Connection string '{InfrastructureSettings.ConnectionStringName}' is missing (env ConnectionStrings__{InfrastructureSettings.ConnectionStringName}).");
}
