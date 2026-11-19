using Microsoft.Extensions.Logging;

namespace NoMorePaper.Web.Client.Classification;

/// <summary>
/// How many documents wait in "Zu prüfen", for the navigation. The number is a convenience: when it
/// cannot be loaded (or the person may not see the list) the navigation simply shows no number.
/// </summary>
public sealed class ReviewCounter(ClassificationApi api, ILogger<ReviewCounter> logger)
{
    public event Action? Changed;

    /// <summary>The number of documents to review, or <c>null</c> when unknown.</summary>
    public int? Count { get; private set; }

    public async Task RefreshAsync()
    {
        // Only the number: this runs on every page change.
        var result = await api.CountReviewAsync();
        if (result.IsSuccess)
        {
            Set(result.Value);
        }
        else
        {
            logger.ReviewCountUnavailable();
            Set(null);
        }
    }

    /// <summary>Takes a count the caller knows already (e.g. after the list changed), without asking the server.</summary>
    public void Set(int? count)
    {
        if (Count == count)
        {
            return;
        }

        Count = count;
        Changed?.Invoke();
    }
}
