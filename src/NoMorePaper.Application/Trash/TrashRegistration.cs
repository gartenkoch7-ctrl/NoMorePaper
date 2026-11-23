using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NoMorePaper.Application.Trash;

/// <summary>Registers the use cases of the trash (ADR-0025); their ports come from Infrastructure.</summary>
public static class TrashRegistration
{
    /// <summary>The trash page of the Server: list, restore, remove for good, empty.</summary>
    public static IServiceCollection AddTrash(this IServiceCollection services)
    {
        services.TryAddScoped<TrashEraser>();
        services.AddScoped<ListTrash>();
        services.AddScoped<RestoreTrashItem>();
        services.AddScoped<RestoreTrashFile>();
        services.AddScoped<PurgeTrashItem>();
        services.AddScoped<EmptyTrash>();
        return services;
    }

    /// <summary>The daily purge of the Worker.</summary>
    public static IServiceCollection AddTrashPurge(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<TrashEraser>();
        services.AddScoped<PurgeDueTrash>();
        return services;
    }
}
