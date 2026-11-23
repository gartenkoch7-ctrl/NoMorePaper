using NoMorePaper.Application.Recognition;

namespace NoMorePaper.Application.Processing;

/// <summary>The recognition stage: <see cref="RecognizeDocument"/> in the job's scope.</summary>
internal sealed class RecognitionStageRunner(RecognizeDocument recognize) : IStageRunner
{
    public Task RunAsync(IJobQueue queue, JobClaim claim, CancellationToken stoppingToken) =>
        recognize.RunAsync(queue, claim, stoppingToken);
}
