using NoMorePaper.Application.Processing;
using NoMorePaper.Infrastructure.Processing;

namespace NoMorePaper.Infrastructure.Tests.Processing;

/// <summary>Claims as a Worker whose model is ready: jobs of every stage.</summary>
internal static class JobQueueExtensions
{
    public static Task<JobClaim?> ClaimNextAsync(this JobQueue queue, TimeSpan lockFor, CancellationToken cancellationToken) =>
        queue.ClaimNextAsync(lockFor, includeRecognition: true, cancellationToken);
}
