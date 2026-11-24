using NoMorePaper.Application.Editing;

namespace NoMorePaper.Infrastructure.Tests.Separation;

/// <summary>One stack of <see cref="SeparationSamples"/>.</summary>
/// <param name="Boundaries">Pages where a new document starts.</param>
/// <param name="SenderOnly">The boundaries only a change of sender shows.</param>
internal sealed record SeparationSample(string Name, IReadOnlyList<PageInfo> Pages, IReadOnlyList<int> Boundaries, IReadOnlyList<int> SenderOnly);
