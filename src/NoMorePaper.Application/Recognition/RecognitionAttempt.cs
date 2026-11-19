using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Recognition;

/// <summary>
/// One recognition attempt of a claimed job: builds the request from the owner's lists and examples,
/// asks the model, checks the answer against the text, matches the names to list entries and takes the
/// fields. Lists and examples are always the document owner's view, so recognition never brings
/// another account's private entries or documents into it; for a shared document only the part the
/// whole household sees already (<see cref="EntryAudience.Household"/>), so it never brings the owner's
/// private-only entries into it either.
/// </summary>
internal sealed class RecognitionAttempt(
    IClassificationStore lists,
    IRecognitionExamples examples,
    IRecognizer recognizer,
    TimeProvider time,
    ILogger logger)
{
    /// <summary>Most confirmed documents sent as examples.</summary>
    public const int MaxExamples = 3;

    public async Task<RecognitionOutcome> RunAsync(IJobQueue queue, JobClaim claim, CancellationToken stoppingToken)
    {
        var document = await queue.ReadAsync(claim, stoppingToken).ConfigureAwait(false);
        if (document is null)
        {
            return new RecognitionOutcome(RecognitionCommit.ClaimLost, SkipReason: null);
        }

        var skip = SkipReason(document);
        if (skip is not null || document is not { OwnerId: { } owner, ContentText: { } text })
        {
            var removed = await queue.CompleteRecognitionAsync(claim, fields: null, wasPrivate: false, wasOwner: null, stoppingToken).ConfigureAwait(false);
            return new RecognitionOutcome(removed, skip ?? "no-text");
        }

        // A shared document gets only what the household sees already (ADR-0023, addendum 2026-11-19):
        // the owner's private-only entries and private examples would otherwise reach it, by name.
        var audience = document.IsPrivate ? EntryAudience.Account : EntryAudience.Household;
        var correspondents = await lists.VisibleEntriesAsync(owner, ClassificationKind.Correspondent, audience, stoppingToken).ConfigureAwait(false);
        var types = await lists.VisibleEntriesAsync(owner, ClassificationKind.DocumentType, audience, stoppingToken).ConfigureAwait(false);
        var tags = await lists.VisibleEntriesAsync(owner, ClassificationKind.Tag, audience, stoppingToken).ConfigureAwait(false);

        var mentioned = RecognitionLists.Mentioned(new NormalizedText(text), correspondents);
        IReadOnlyList<RecognitionExample> found = mentioned.Count == 0
            ? []
            : await examples.FindAsync(owner, audience, [.. mentioned.Select(entry => entry.Id)], document.Id, MaxExamples, stoppingToken)
                .ConfigureAwait(false);
        var request = new RecognitionRequest(
            RecognitionText.Shorten(text),
            RecognitionLists.Names(correspondents, mentioned, RecognitionLists.MaxCorrespondents),
            [.. types.Select(entry => entry.Name)],
            RecognitionLists.Names(tags, [], RecognitionLists.MaxTags),
            found);

        var answer = await recognizer.RecognizeAsync(request, stoppingToken).ConfigureAwait(false);

        // The model took a while: if the job or the document changed meanwhile, the answer is not used
        // and no list entries are created for it. Only a change in the moments until the commit below
        // still discards it after its new entries were made; they stay unused, seen by the owner only.
        switch (await queue.ReadAsync(claim, stoppingToken).ConfigureAwait(false))
        {
            case null:
                return new RecognitionOutcome(RecognitionCommit.ClaimLost, SkipReason: null);
            case { Recognition: RecognitionState.Confirmed }:
                var removed = await queue.CompleteRecognitionAsync(claim, fields: null, wasPrivate: false, wasOwner: null, stoppingToken).ConfigureAwait(false);
                return new RecognitionOutcome(removed, SkipReason: null);
            case { OwnerId: var current } when current != owner:
                // Handed over meanwhile: the request held the previous owner's lists. The job runs again
                // with the new owner's (the hand-over queues it anew; this only covers the gap).
                var handedBack = await queue.ReleaseAsync(claim, stoppingToken).ConfigureAwait(false);
                return new RecognitionOutcome(handedBack ? RecognitionCommit.OwnerChanged : RecognitionCommit.ClaimLost, SkipReason: null);
            case { IsPrivate: false } when audience == EntryAudience.Account:
                // Shared meanwhile: the request held the owner's private view, so the answer may name a
                // private-only entry. The job runs again at once, with the household's view.
                var released = await queue.ReleaseAsync(claim, stoppingToken).ConfigureAwait(false);
                return new RecognitionOutcome(released ? RecognitionCommit.SharedMeanwhile : RecognitionCommit.ClaimLost, SkipReason: null);
        }

        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var (result, rejected) = RecognitionChecks.Apply(answer, text, correspondents, today);
        foreach (var field in rejected)
        {
            logger.ValueRejected(field, document.Id);
            RecognitionLog.RejectedValues.Add(1, new KeyValuePair<string, object?>(RecognitionLog.FieldTag, field));
        }

        var fields = await ToFieldsAsync(owner, audience, result, stoppingToken).ConfigureAwait(false);
        // Checked again under the row lock: sharing during FindOrCreate also hands the job back.
        var commit = await queue.CompleteRecognitionAsync(claim, fields, wasPrivate: document.IsPrivate, wasOwner: owner, stoppingToken).ConfigureAwait(false);
        return new RecognitionOutcome(commit, SkipReason: null, found.Count, rejected.Count);
    }

    private static string? SkipReason(Document document) => document switch
    {
        { Recognition: RecognitionState.Confirmed } => "confirmed",
        { OwnerId: null } => "no-owner",
        { ContentText: var text } when string.IsNullOrWhiteSpace(text) => "no-text",
        _ => null,
    };

    // Names become entries the audience may see: an equal one it sees already, or a new one of the owner's.
    private async Task<DocumentFields> ToFieldsAsync(Guid owner, EntryAudience audience, RecognitionResult result, CancellationToken stoppingToken)
    {
        var correspondent = await EntryAsync(owner, ClassificationKind.Correspondent, result.Correspondent, audience, stoppingToken).ConfigureAwait(false);
        var documentType = await EntryAsync(owner, ClassificationKind.DocumentType, result.DocumentType, audience, stoppingToken).ConfigureAwait(false);
        var tagIds = new List<Guid>();
        foreach (var tag in result.Tags)
        {
            if (await EntryAsync(owner, ClassificationKind.Tag, tag, audience, stoppingToken).ConfigureAwait(false) is { } id)
            {
                tagIds.Add(id);
            }
        }

        return new DocumentFields(result.Title, correspondent, documentType, result.DocumentDate, result.Amount, result.Summary, tagIds);
    }

    private async Task<Guid?> EntryAsync(Guid owner, ClassificationKind kind, string? name, EntryAudience audience, CancellationToken stoppingToken) =>
        name is null ? null : (await lists.FindOrCreateAsync(owner, kind, name, audience, stoppingToken).ConfigureAwait(false)).Id;
}
