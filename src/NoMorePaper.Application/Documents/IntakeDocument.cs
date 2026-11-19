using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Telemetry;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Documents;

/// <summary>
/// Accepts a new document for an explicit owner, without a signed-in account: the core of every input
/// (browser upload through <see cref="ReceiveDocument"/>, scanner folder, mail, iCloud; ADR-0024).
/// Stores the original in the inbox and records document, file and queue entry in one transaction. A
/// file already there in the given <see cref="DuplicateScope"/> is not kept. If the database refuses, the
/// inbox file is removed again, but only once it is certain that no document refers to it: a lost
/// original is worse than a leftover file.
/// </summary>
public sealed class IntakeDocument(
    IDocumentRepository documents,
    IDocumentStorage storage,
    IAccountStore accounts,
    IAccountPreferencesStore preferences,
    TimeProvider clock,
    ILogger<IntakeDocument> logger)
{
    private const string SourceTag = "nomorepaper.inputs.source";

    private static readonly Counter<long> s_received = ApplicationTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.documents.received", description: "Documents accepted by the intake, by source");

    private static readonly Counter<long> s_skipped = ApplicationTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.inputs.skipped", description: "Files the intake did not take, by source and reason");

    /// <param name="isPrivate">
    /// Whether only the owner may see it, as chosen per file in the browser; <c>null</c> takes the owner's
    /// preference. A document of unknown owner is private in any case.
    /// </param>
    /// <param name="cancellationToken">Cancels storing the file; once the file is stored, the commit runs to its end.</param>
    public async Task<IntakeResult> AcceptAsync(IntakeRequest request, bool? isPrivate, DuplicateScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Content);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MediaType);
        using var activity = ApplicationTelemetry.Source.StartActivity("nomorepaper.documents.receive");

        var source = request.Source;
        Guid owner;
        if (request.OwnerId is { } given)
        {
            if (!await accounts.ExistsAsync(given, cancellationToken).ConfigureAwait(false))
            {
                logger.OwnerGone(source, given);
                Skipped(source, "owner-gone");
                return IntakeResult.OwnerGone;
            }

            owner = given;
        }
        else if (await accounts.FindAdministratorIdAsync(cancellationToken).ConfigureAwait(false) is { } administrator)
        {
            owner = administrator;
        }
        else
        {
            logger.NoAdministrator(source);
            Skipped(source, "no-administrator");
            return IntakeResult.NoAdministrator;
        }

        var makePrivate = isPrivate ?? (await preferences.GetAsync(owner, cancellationToken).ConfigureAwait(false)).NewDocumentsPrivate;
        var document = Document.Receive(DocumentNames.Title(request.FileName), clock.GetUtcNow(), owner, makePrivate, source);
        if (request.Mail is { } mail)
        {
            document.AttachMailOrigin(mail);
        }

        if (request.OwnerId is null)
        {
            document.MarkOwnerCheck();
        }

        return await StoreAsync(document, request, scope, activity, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IntakeResult> StoreAsync(
        Document document, IntakeRequest request, DuplicateScope scope, Activity? activity, CancellationToken cancellationToken)
    {
        var owner = document.OwnerId!.Value;
        var cleanName = DocumentNames.FileName(request.FileName);
        var committing = false;
        try
        {
            var stored = await storage.SaveInboxAsync(document.Id, cleanName, request.Content, cancellationToken).ConfigureAwait(false);

            // The hash is computed while storing, so the file is read only once. Only documents the owner
            // may see count: a refusal must not reveal another account's private document.
            var existing = scope == DuplicateScope.Visible
                ? await documents.FindVisibleBySha256Async(owner, stored.Sha256, cancellationToken).ConfigureAwait(false)
                : await documents.FindOwnedBySha256Async(owner, stored.Sha256, cancellationToken).ConfigureAwait(false);
            if (existing is { } existingId)
            {
                DiscardInbox(document.Id);
                if (scope == DuplicateScope.Visible)
                {
                    logger.DuplicateRefused(existingId, owner);
                }
                else
                {
                    logger.DuplicateDropped(request.Source, owner, existingId);
                }

                Skipped(request.Source, "duplicate");
                return IntakeResult.AlreadyThere(existingId);
            }

            document.AttachFile(new DocumentFile(DocumentFileKind.Original, cleanName, request.MediaType, stored.SizeBytes, stored.Sha256));

            // A dropped request must not cancel a commit that may already have reached the database:
            // the caller would see a failure for a document that exists, and its file would be discarded.
            committing = true;
            await documents.AddReceivedAsync(document, CancellationToken.None).ConfigureAwait(false);

            s_received.Add(1, new KeyValuePair<string, object?>(SourceTag, request.Source.ToString()));
            logger.Received(document.Id, request.Source, stored.SizeBytes, request.MediaType);
            return IntakeResult.Received(document.Id);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            if (exception is not OperationCanceledException)
            {
                logger.ReceiveFailed(document.Id, request.Source, exception.GetType().Name);
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
            var recorded = await documents.ExistsAsync(id, CancellationToken.None).ConfigureAwait(false);
            if (recorded)
            {
                logger.KeptRecorded(id);
            }

            return !recorded;
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException or TimeoutException)
        {
            logger.KeptUnverified(id, exception.GetType().Name);
            return false;
        }
    }

    private void DiscardInbox(Guid id)
    {
        try
        {
            storage.DiscardInbox(id);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The original exception is the one worth reporting; a leftover only costs space.
            logger.DiscardFailed(id, exception.GetType().Name);
        }
    }

    private static void Skipped(DocumentSource source, string reason) =>
        s_skipped.Add(1, new KeyValuePair<string, object?>(SourceTag, source.ToString()), new KeyValuePair<string, object?>("nomorepaper.inputs.reason", reason));
}
