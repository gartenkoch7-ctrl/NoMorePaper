using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Processing;
using NoMorePaper.Infrastructure.Telemetry;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>
/// Accepts a new document: stores the original in the inbox, records document, file and queue entry
/// in one database transaction. If the database refuses, the inbox file is removed again, so no
/// file is left that no document refers to.
/// </summary>
public sealed class DocumentIntake
{
    private static readonly Counter<long> s_received = InfrastructureTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.documents.received", description: "Documents accepted by the intake");

    private readonly AppDbContext _db;
    private readonly IDocumentStorage _storage;
    private readonly TimeProvider _clock;
    private readonly DocumentPaths _paths;
    private readonly ILogger<DocumentIntake> _logger;

    /// <param name="dataRoot">The storage root; used only to remove the inbox folder after a failure.</param>
    public DocumentIntake(
        AppDbContext db, IDocumentStorage storage, TimeProvider clock, string dataRoot, ILogger<DocumentIntake> logger)
    {
        _db = db;
        _storage = storage;
        _clock = clock;
        _paths = new DocumentPaths(dataRoot);
        _logger = logger;
    }

    /// <param name="fileName">Name the file was received with; cleaned here, so callers may pass it unchecked.</param>
    /// <param name="mediaType">The detected type of the content, not what the uploader claimed.</param>
    /// <returns>The id of the new, queued document.</returns>
    public async Task<Guid> ReceiveAsync(string fileName, string mediaType, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        using var activity = InfrastructureTelemetry.Source.StartActivity("nomorepaper.documents.receive");

        var now = _clock.GetUtcNow();
        var cleanName = DocumentNames.FileName(fileName);
        var document = Document.Receive(DocumentNames.Title(fileName), now);
        try
        {
            var stored = await _storage.SaveInboxAsync(document.Id, cleanName, content, cancellationToken).ConfigureAwait(false);
            document.AttachFile(new DocumentFile(DocumentFileKind.Original, cleanName, mediaType, stored.SizeBytes, stored.Sha256));
            _db.Documents.Add(document);
            _db.ProcessingJobs.Add(new ProcessingJob { DocumentId = document.Id, Attempt = 0, NextAttemptAt = now });
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            s_received.Add(1);
            _logger.Received(document.Id, stored.SizeBytes, mediaType);
            return document.Id;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            if (exception is not OperationCanceledException)
            {
                _logger.ReceiveFailed(document.Id, exception.GetType().Name);
            }

            DiscardInbox(document.Id);
            throw;
        }
    }

    private void DiscardInbox(Guid id)
    {
        try
        {
            var folder = _paths.InboxFolder(id);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The original exception is the one worth reporting; a leftover only costs space.
            _logger.DiscardFailed(id, exception.GetType().Name);
        }
    }
}
