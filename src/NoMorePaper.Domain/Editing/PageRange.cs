namespace NoMorePaper.Domain.Editing;

/// <summary>Consecutive pages of a document, both ends included, starting at 1.</summary>
public sealed record PageRange
{
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="first"/> is below 1 or <paramref name="last"/> below <paramref name="first"/>.</exception>
    public PageRange(int first, int last)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(first, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(last, first);
        First = first;
        Last = last;
    }

    public int First { get; }

    public int Last { get; }

    /// <summary>The pages from <see cref="First"/> to <see cref="Last"/>.</summary>
    public IEnumerable<int> Pages() => Enumerable.Range(First, Last - First + 1);
}
