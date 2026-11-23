using NoMorePaper.Application.Classification;
using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Application.Editing;

/// <summary>
/// Splits a processed document into several new ones (ADR-0025): who may edit the fields may split it;
/// the Worker makes the parts, which are recognized anew, and then moves the document to the trash. Used
/// by "Seiten bearbeiten" (with cropped pages, in one "Speichern") and by an accepted split suggestion.
/// </summary>
public sealed class SplitDocument(RequestPageEdit requests) : ISplitRequester
{
    /// <inheritdoc />
    /// <remarks>
    /// Without images: a plan with replaced pages is invalid here. A refusal commits nothing and takes back
    /// what it wrote into the open change (see <see cref="ISplitRequester"/>).
    /// </remarks>
    public Task<ChangeOutcome> RequestAsync(Guid id, SplitPlan plan, CancellationToken cancellationToken) =>
        RequestAsync(id, plan, [], cancellationToken);

    /// <param name="images">One JPEG per replaced page of all parts, none else (as for <see cref="RequestPageEdit"/>).</param>
    /// <returns>As <see cref="ISplitRequester.RequestAsync"/>; a refusal likewise commits nothing.</returns>
    public Task<ChangeOutcome> RequestAsync(Guid id, SplitPlan plan, IReadOnlyList<EditPageImage> images, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(images);
        IReadOnlyList<IReadOnlyList<PageStep>> parts = [.. (plan.Parts ?? []).Select(part => part?.Steps ?? [])];
        return requests.QueueAsync(
            id,
            EditKind.Split,
            parts.SelectMany(steps => steps),
            plan.Validate,
            data => data with { Parts = parts },
            images,
            cancellationToken);
    }
}
