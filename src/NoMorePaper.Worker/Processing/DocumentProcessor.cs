using NoMorePaper.Application.Processing;

namespace NoMorePaper.Worker.Processing;

/// <summary>
/// Works through the processing queue: prepares the working folder once, then lets
/// <see cref="ProcessNextDocument"/> take every due job and waits for the next tick. On shutdown
/// the use case hands the current job back.
/// </summary>
internal sealed class DocumentProcessor(
    ProcessNextDocument processNext,
    WorkingFolder workingFolder,
    TimeProvider time,
    ILogger<DocumentProcessor> logger) : BackgroundService
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        workingFolder.Prepare();
        logger.ProcessingStarted((int)PollInterval.TotalSeconds);
        using var timer = new PeriodicTimer(PollInterval, time);
        do
        {
            // Work through everything that is due, then wait for the next tick.
            while (await processNext.ProcessNextAsync(stoppingToken).ConfigureAwait(false))
            {
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
