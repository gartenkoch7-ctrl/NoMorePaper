using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Editing;
using NoMorePaper.Infrastructure.Classification;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Documents.Pdf;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Inputs;
using NoMorePaper.Worker.Tests.Processing;

namespace NoMorePaper.Worker.Tests.Editing;

/// <summary>What the edit stage needs besides the processing services of <see cref="ProcessorHarness"/>: real stores, fake qpdf.</summary>
internal static class EditingServices
{
    public static void Add(IServiceCollection services, string dataRoot, FakePdfTool pdf)
    {
        services.AddLogging();
        services.AddScoped<IDocumentEdits, EfDocumentEdits>();
        services.AddScoped<IDocumentPages, EfDocumentPages>();
        services.AddScoped<IAccountPreferencesStore, EfAccountPreferencesStore>();
        services.AddScoped<IDocumentFieldsStore, EfDocumentFieldsStore>();
        services.AddScoped<IDocumentRepository, EfDocumentRepository>();
        services.AddSingleton<IAccountStore>(new EveryAccountExists());
        services.AddSingleton<IPdfTool>(pdf);
        services.AddSingleton<IPdfComposer>(provider => new PdfSharpComposer(TimeProvider.System, NullLogger<PdfSharpComposer>.Instance));
        services.AddSingleton<IEditPageStorage>(new EditPageStorage(dataRoot, new SkippedDirectorySync(), NullLogger<EditPageStorage>.Instance));
    }
}
