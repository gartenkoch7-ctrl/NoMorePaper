using NoMorePaper.Contracts.Editing;

namespace NoMorePaper.Web.Client.Editing;

/// <summary>
/// The state of the page editor: the pages in their order with what is done to each, the split points
/// and an undo stack. Pure logic; the page turns it into <see cref="PagePlanDto"/> or <see cref="SplitPlanDto"/>.
/// Split points are positions: a split "after index i" stays at i when pages move.
/// </summary>
public sealed class PagePlanModel
{
    private readonly record struct Snapshot(IReadOnlyList<PageEntry> Entries, IReadOnlySet<int> Splits);

    private readonly Stack<Snapshot> _undo = new();
    private readonly Snapshot _initial;
    private Snapshot _now;

    public PagePlanModel(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageCount);
        _initial = new Snapshot(
            Enumerable.Range(1, pageCount).Select(page => new PageEntry(page)).ToList(), new HashSet<int>());
        _now = _initial;
    }

    public IReadOnlyList<PageEntry> Entries => _now.Entries;

    /// <summary>Indexes of the entries a document ends after.</summary>
    public IReadOnlySet<int> SplitsAfter => _now.Splits;

    public bool IsDirty => !_now.Entries.SequenceEqual(_initial.Entries) || !_now.Splits.SetEquals(_initial.Splits);

    public bool CanUndo => _undo.Count > 0;

    /// <summary>Pages that stay (not deleted): the "y" of "Seite x von y".</summary>
    public int KeptCount => _now.Entries.Count(entry => !entry.Deleted);

    /// <summary>Saving needs a change and at least one page that stays.</summary>
    public bool CanSave => IsDirty && KeptCount > 0;

    /// <summary>Documents the pages are saved as: parts between split points that keep at least one page.</summary>
    public int PartCount => Parts().Count;

    /// <summary>True when saving splits the document (at least two parts keep pages).</summary>
    public bool IsSplit => PartCount >= 2;

    /// <summary>Number of the page among those that stay, counting up to entry <paramref name="index"/> (a deleted page gets its predecessor's).</summary>
    public int NumberOf(int index) => _now.Entries.Take(index + 1).Count(entry => !entry.Deleted);

    /// <summary>The entry was moved from its stored position (its source page differs from its place in the list).</summary>
    public bool IsMoved(int index) => _now.Entries[index].SourcePage != index + 1;

    /// <summary>Number (from 1) of the document that ends at the split point after entry <paramref name="index"/>.</summary>
    public int DocumentNumberOfSplit(int index) => _now.Splits.Count(split => split <= index);

    public void RotateRight(int index) => Rotate(index, true);

    public void RotateLeft(int index) => Rotate(index, false);

    public void ToggleDelete(int index) => Change(index, entry => entry with { Deleted = !entry.Deleted });

    public void Replace(int index, string jpegKey, string cropText)
        => Change(index, entry => entry with { ReplacedJpegKey = jpegKey, CropText = cropText });

    public void ClearCrop(int index) => Change(index, entry => entry with { ReplacedJpegKey = null, CropText = null });

    /// <summary>Moves the entry up one place; false for the first one.</summary>
    public bool MoveUp(int index) => Move(index, index - 1);

    /// <summary>Moves the entry down one place; false for the last one.</summary>
    public bool MoveDown(int index) => Move(index, index + 1);

    /// <summary>Sets or removes the split point after entry <paramref name="index"/>; none after the last entry.</summary>
    public bool ToggleSplitAfter(int index)
    {
        if (index < 0 || index >= _now.Entries.Count - 1)
        {
            return false;
        }

        var splits = new HashSet<int>(_now.Splits);
        if (!splits.Remove(index))
        {
            splits.Add(index);
        }

        Push(new Snapshot(_now.Entries, splits));
        return true;
    }

    /// <summary>Undoes the last change; false when there is none.</summary>
    public bool Undo()
    {
        if (!_undo.TryPop(out var previous))
        {
            return false;
        }

        _now = previous;
        return true;
    }

    /// <summary>Drops every change (after "Alles verwerfen").</summary>
    public void Reset()
    {
        _undo.Clear();
        _now = _initial;
    }

    /// <summary>Sets split points between the suggested parts and deletes the blank pages (the "?trennen=1" start).</summary>
    public void FromSuggestion(SplitSuggestionDto suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);
        var entries = _now.Entries
            .Select(entry => suggestion.BlankPages.Contains(entry.SourcePage) ? entry with { Deleted = true } : entry)
            .ToList();
        var splits = new HashSet<int>(_now.Splits);
        foreach (var part in suggestion.Parts.Take(suggestion.Parts.Count - 1))
        {
            var index = entries.FindIndex(entry => entry.SourcePage == part.Last);
            if (index >= 0 && index < entries.Count - 1)
            {
                splits.Add(index);
            }
        }

        Push(new Snapshot(entries, splits));
    }

    /// <summary>The plan of one document; deleted pages are left out. Use when <see cref="IsSplit"/> is false.</summary>
    public PagePlanDto ToPlan() => ToPlan(_now.Entries);

    /// <summary>The plan of the parts between the split points; parts without a page are left out.</summary>
    public SplitPlanDto ToSplit() => new([.. Parts().Select(ToPlan)]);

    private static PagePlanDto ToPlan(IEnumerable<PageEntry> entries) => new(
        [.. entries.Where(entry => !entry.Deleted).Select(entry => new PageStepDto(entry.SourcePage, entry.Rotation, entry.ReplacedJpegKey is not null))]);

    private List<List<PageEntry>> Parts()
    {
        var parts = new List<List<PageEntry>> { new() };
        for (var i = 0; i < _now.Entries.Count; i++)
        {
            parts[^1].Add(_now.Entries[i]);
            if (_now.Splits.Contains(i))
            {
                parts.Add([]);
            }
        }

        return [.. parts.Where(part => part.Any(entry => !entry.Deleted))];
    }

    private void Rotate(int index, bool right) => Change(index, entry => entry with
    {
        Rotation = (PageRotationDto)(((int)entry.Rotation + (right ? 90 : 270)) % 360),
    });

    private void Change(int index, Func<PageEntry, PageEntry> change)
    {
        if (index < 0 || index >= _now.Entries.Count)
        {
            return;
        }

        var entries = _now.Entries.ToList();
        entries[index] = change(entries[index]);
        Push(new Snapshot(entries, _now.Splits));
    }

    private bool Move(int from, int to)
    {
        if (from < 0 || from >= _now.Entries.Count || to < 0 || to >= _now.Entries.Count)
        {
            return false;
        }

        var entries = _now.Entries.ToList();
        (entries[from], entries[to]) = (entries[to], entries[from]);
        Push(new Snapshot(entries, _now.Splits));
        return true;
    }

    private void Push(Snapshot next)
    {
        _undo.Push(_now);
        _now = next;
    }
}
