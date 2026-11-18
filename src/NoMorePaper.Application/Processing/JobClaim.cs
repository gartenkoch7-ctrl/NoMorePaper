namespace NoMorePaper.Application.Processing;

/// <summary>A Worker's hold on a job, returned by <see cref="IJobQueue.ClaimNextAsync"/>.</summary>
/// <param name="DocumentId">The job's document.</param>
/// <param name="ClaimId">
/// Token of this claim, new for every claim; every later call of the queue acts only while the job
/// still carries it.
/// </param>
/// <param name="Attempt">Attempt number this claim counts as (1 for the first).</param>
public sealed record JobClaim(Guid DocumentId, Guid ClaimId, int Attempt);
