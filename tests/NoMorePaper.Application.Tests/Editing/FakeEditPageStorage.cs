using NoMorePaper.Application.Editing;

namespace NoMorePaper.Application.Tests.Editing;

/// <summary>Page images in memory, by document, edit and page.</summary>
internal sealed class FakeEditPageStorage : IEditPageStorage
{
    public Dictionary<(Guid Id, Guid EditId, int Page), byte[]> Images { get; } = [];

    public async Task SaveAsync(Guid id, Guid editId, int page, Stream jpeg, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        await jpeg.CopyToAsync(copy, cancellationToken);
        Images[(id, editId, page)] = copy.ToArray();
    }

    public Stream OpenRead(Guid id, Guid editId, int page) => new MemoryStream(Images[(id, editId, page)]);

    public void Discard(Guid id, Guid editId)
    {
        foreach (var key in Images.Keys.Where(key => key.Id == id && key.EditId == editId).ToList())
        {
            Images.Remove(key);
        }
    }
}
