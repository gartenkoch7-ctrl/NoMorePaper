using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
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
/// file is left that no document refers to. It is only removed once it is certain that no document
/// refers to it: a lost original is worse than a leftover file.
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
    /// <param name="cancellationToken">Cancels storing the file; once the file is stored, the commit runs to its end.</param>
    /// <returns>The id of the new, queued document.</returns>
    public async Task<Guid> ReceiveAsync(string fileName, string mediaType, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        using var activity = InfrastructureTelemetry.Source.StartActivity("nomorepaper.documents.receive");

        var now = _clock.GetUtcNow();
        var cleanName = DocumentNames.FileName(fileName);
        var document = Document.Receive(DocumentNames.Title(fileName), now);
        var committing = false;
        try
        {
            var stored = await _storage.SaveInboxAsync(document.Id, cleanName, content, cancellationToken).ConfigureAwait(false);
            document.AttachFile(new DocumentFile(DocumentFileKind.Original, cleanName, mediaType, stored.SizeBytes, stored.Sha256));
            _db.Documents.Add(document);
            _db.ProcessingJobs.Add(new ProcessingJob { DocumentId = document.Id, Attempt = 0, NextAttemptAt = now });

            // A dropped request must not cancel a commit that may already have reached the database:
            // the caller would see a failure for a document that exists, and its file would be discarded.
            committing = true;
            await _db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

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

            if (!committing || await IsUnrecordedAsync(document.Id).ConfigureAwait(false))
            {
                DiscardInbox(document.Id);
            }

            throw;
        }
    }

    // A failed commit may still have been applied, e.g. when the connection dropped before the answer.
    private async Task<bool> IsUnrecordedAsync(Guid id)
    {
        try
        {
            var recorded = await _db.Documents.AsNoTracking()
                .AnyAsync(d => d.Id == id, CancellationToken.None).ConfigureAwait(false);
            if (recorded)
            {
                _logger.KeptRecorded(id);
            }

            return !recorded;
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException or TimeoutException)
        {
            _logger.KeptUnverified(id, exception.GetType().Name);
            return false;
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
