using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Processing;

namespace NoMorePaper.Worker.Processing;

/// <summary>
/// One processing attempt of a claimed job: marks the document as processing, runs OCR on a
/// working copy of the original, stores the archive PDF, moves the original into the archive and
/// completes the document. Every database step checks the claim, so a Worker that lost its job to
/// another one stops without changing it.
/// </summary>
internal sealed class DocumentPipeline(IDocumentStorage storage, IOcrEngine ocr)
{
    /// <param name="work">Empty working folder of this attempt; the caller deletes it.</param>
    /// <returns>The OCR result, or <c>null</c> when the claim was lost and the result is discarded.</returns>
    public async Task<OcrResult?> RunAsync(JobQueue queue, JobClaim claim, string work, CancellationToken stoppingToken)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(claim);
        var id = claim.DocumentId;
        var document = await queue.StartAsync(claim, stoppingToken).ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        var original = document.FindFile(DocumentFileKind.Original)
            ?? throw new InvalidOperationException("The document has no original file.");

        // The working copy has a fixed name: the received name never reaches a tool's command line.
        var input = Path.Combine(work, "original");
        var source = storage.OpenRead(id, DocumentFileKind.Original);
        await using (source.ConfigureAwait(false))
        {
            var copy = File.Create(input);
            await using (copy.ConfigureAwait(false))
            {
                await source.CopyToAsync(copy, stoppingToken).ConfigureAwait(false);
            }
        }

        var output = Path.Combine(work, "archive.pdf");
        var result = await ocr.CreateArchiveAsync(input, original.MediaType, output, stoppingToken).ConfigureAwait(false);

        // Files are not guarded by the claim: only a Worker far past its lock (longer than OCR's time
        // limit) could still replace the archive PDF here, and its completion is then rejected.
        StoredFile archive;
        var pdf = File.OpenRead(output);
        await using (pdf.ConfigureAwait(false))
        {
            archive = await storage.SaveArchiveAsync(id, DocumentFileKind.Archive, pdf, stoppingToken).ConfigureAwait(false);
        }

        // Moving the original and completing are both safe to repeat if the attempt fails in between.
        await storage.ArchiveOriginalAsync(id, stoppingToken).ConfigureAwait(false);
        var completed = await queue.CompleteAsync(claim, result.Text, archive, stoppingToken).ConfigureAwait(false);
        return completed ? result : null;
    }
}
