using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Recognition;

namespace NoMorePaper.Application.Separation;

/// <summary>
/// Asks the local LLM where a scanned stack splits (M4b, stack separation): it reads only the start of
/// each page and names each page's sender. One request per stack, with the priority of recognition, since
/// the asking stage holds a job's lock like recognition does.
/// </summary>
public interface ISeparationAssessor
{
    /// <param name="pages">The pages of the stack, ordered by number.</param>
    /// <exception cref="LlmBusyException">The LLM stayed busy too long; nothing was asked.</exception>
    /// <remarks>Any other exception means the LLM gave no usable answer (unreachable, out of time, unusable answer).</remarks>
    Task<SeparationAssessment> AssessAsync(IReadOnlyList<PageInfo> pages, CancellationToken cancellationToken);
}
