using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Recognition;

/// <summary>
/// After OCR: marks the document as a possible duplicate of the most similar document its owner may
/// see, if their texts are nearly the same (M3 plan, "Inhaltliche Duplikate"). A failed check only
/// leaves the document without hint; it never fails processing.
/// </summary>
public sealed class FindContentDuplicate(IDuplicateFinder finder, ILogger<FindContentDuplicate> logger)
{
    /// <summary>Least trigram similarity of two texts (0 to 1) for the hint.</summary>
    public const double Threshold = 0.9;

    /// <summary>
    /// Shorter texts are not compared: a photo with a few recognized words would look like every other.
    /// </summary>
    public const int MinCharacters = 50;

    /// <param name="text">The text OCR just recognized.</param>
    /// <exception cref="OperationCanceledException">The Worker is stopping.</exception>
    public async Task RunAsync(Guid documentId, string text, CancellationToken stoppingToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Trim().Length < MinCharacters)
        {
            return;
        }

        try
        {
            if (await finder.FindSimilarAsync(documentId, Threshold, stoppingToken).ConfigureAwait(false) is { } other
                && await finder.MarkAsync(documentId, other, stoppingToken).ConfigureAwait(false))
            {
                RecognitionLog.Duplicates.Add(1);
                logger.PossibleDuplicate(documentId, other);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.DuplicateCheckFailed(documentId, exception.GetType().Name);
        }
    }
}
