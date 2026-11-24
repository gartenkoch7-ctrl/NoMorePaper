using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Application.Inputs.Mail;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Application.Search;
using NoMorePaper.Application.Separation;
using NoMorePaper.Application.Trash;

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
        services.AddScoped<IntakeDocument>();
        services.AddScoped<ReceiveDocument>();
        services.AddScoped<ListDocuments>();
        services.AddScoped<CountDocuments>();
        services.AddScoped<GetDocument>();
        services.AddScoped<GetDocumentText>();
        services.AddScoped<OpenDocumentFile>();
        services.AddScoped<SearchDocuments>();
        services.AddScoped<SetDocumentPrivacy>();
        services.AddScoped<MoveDocumentToTrash>();
        services.AddTrash();
        services.AddScoped<ListInputStatus>();
        services.AddScoped<ConfigureMailbox>();
        services.AddAccounts();
        services.AddClassification();
        return services;
    }

    private static void AddClassification(this IServiceCollection services)
    {
        services.AddScoped<GetDocumentFields>();
        services.AddScoped<EditDocumentFields>();
        services.AddScoped<ConfirmDocument>();
        services.AddScoped<SetDocumentEditors>();
        services.AddScoped<KeepDuplicate>();
        services.AddScoped<DeleteDuplicate>();
        services.AddScoped<ListReview>();
        services.AddScoped<ListEntries>();
        services.AddScoped<CreateEntry>();
        services.AddScoped<RenameEntry>();
        services.AddScoped<MergeEntries>();
        services.AddScoped<ConfirmOwnerCheck>();
        services.AddScoped<HandOverDocument>();
        services.AddScoped<AnswerSplitSuggestion>();
    }

    private static void AddAccounts(this IServiceCollection services)
    {
        services.TryAddSingleton<ISecretGenerator, RandomSecretGenerator>();
        services.AddScoped<SetUpHousehold>();
        services.AddScoped<SignIn>();
        services.AddScoped<GetAccount>();
        services.AddScoped<SignOutEverywhere>();
        services.AddScoped<GetAdminResetNotice>();
        services.AddScoped<AcknowledgeAdminReset>();
        services.AddScoped<ChangePassword>();
        services.AddScoped<InviteMember>();
        services.AddScoped<AcceptInvitation>();
        services.AddScoped<ListMembers>();
        services.AddScoped<ListHousehold>();
        services.AddScoped<RemoveMember>();
        services.AddScoped<CreateResetLink>();
        services.AddScoped<ResetPassword>();
        services.AddScoped<ResetWithEmergencyCode>();
        services.AddScoped<GetPreferences>();
        services.AddScoped<ChangePreferences>();
        services.AddScoped<GetAfterEditing>();
        services.AddScoped<ChangeAfterEditing>();
    }

    /// <summary>
    /// The processing use cases (Worker): OCR, recognition, content duplicates, the backlog, and the
    /// intake of the background inputs. Need <see cref="IOcrEngine"/>, <see cref="IWorkingFolder"/>,
    /// <see cref="IRecognizer"/>, <see cref="IRecognitionExamples"/> and <see cref="IDuplicateFinder"/>, which
    /// only the Worker provides, hence apart from <see cref="AddApplication"/>; the intake needs
    /// <see cref="IAccountStore"/>, which the Worker registers too.
    /// </summary>
    public static IServiceCollection AddDocumentProcessing(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ProcessNextDocument>();
        services.AddSingleton<RecognitionReadiness>();
        services.AddScoped<RecognizeDocument>();
        services.AddScoped<FindContentDuplicate>();
        services.AddScoped<EnqueueBacklog>();
        services.AddTrashPurge();
        services.AddStageRunners();

        // The background inputs (ADR-0024) take files in without a signed-in account.
        services.AddScoped<IntakeDocument>();

        // The mail input asks the local LLM (IMailAssessor), which only the Worker provides.
        services.AddScoped<MailIntake>();
        services.AddScoped<ProcessMailbox>();
        return services;
    }

    /// <summary>
    /// The runner of each <see cref="JobStage"/>, keyed by the stage (<see cref="ProcessNextDocument"/>).
    /// Part of <see cref="AddDocumentProcessing"/>; the Worker tests wire it on its own.
    /// </summary>
    public static IServiceCollection AddStageRunners(this IServiceCollection services)
    {
        services.AddKeyedScoped<IStageRunner, OcrStageRunner>(JobStage.Ocr);
        services.AddKeyedScoped<IStageRunner, RecognitionStageRunner>(JobStage.Recognition);
        services.AddKeyedScoped<IStageRunner, SeparationStageRunner>(JobStage.Separation);
        return services;
    }
}
