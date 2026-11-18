using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Search;

namespace NoMorePaper.Application.Composition;

/// <summary>Registers the use cases in the composition roots; their ports come from Infrastructure and the hosts.</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Use cases of the HTTP API (Server): intake, list, detail, download, privacy and search of
    /// documents, and the accounts. Needs <see cref="ICurrentAccount"/>, which the Server provides.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ReceiveDocument>();
        services.AddScoped<ListDocuments>();
        services.AddScoped<GetDocument>();
        services.AddScoped<OpenDocumentFile>();
        services.AddScoped<SearchDocuments>();
        services.AddScoped<SetDocumentPrivacy>();
        services.AddAccounts();
        return services;
    }

    private static void AddAccounts(this IServiceCollection services)
    {
        services.TryAddSingleton<ISecretGenerator, RandomSecretGenerator>();
        services.AddScoped<SetUpHousehold>();
        services.AddScoped<SignIn>();
        services.AddScoped<GetAccount>();
        services.AddScoped<SignOutEverywhere>();
        services.AddScoped<AcknowledgeAdminReset>();
        services.AddScoped<ChangePassword>();
        services.AddScoped<InviteMember>();
        services.AddScoped<AcceptInvitation>();
        services.AddScoped<ListMembers>();
        services.AddScoped<RemoveMember>();
        services.AddScoped<CreateResetLink>();
        services.AddScoped<ResetPassword>();
        services.AddScoped<ResetWithEmergencyCode>();
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
