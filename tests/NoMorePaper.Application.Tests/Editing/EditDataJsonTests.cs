using NoMorePaper.Application.Editing;
using NoMorePaper.Domain.Editing;
using Xunit;

namespace NoMorePaper.Application.Tests.Editing;

/// <summary>The plan of an edit as stored in <c>document_edits.data</c>.</summary>
public sealed class EditDataJsonTests
{
    private static readonly Guid s_edit = Guid.CreateVersion7();

    [Fact]
    public void APagePlan_RoundTrips_WithRotationsByName()
    {
        var data = new EditData(s_edit) { Steps = [new PageStep(2, PageRotation.Right, true)] };

        var json = EditDataJson.ToJson(data);
        var parsed = EditDataJson.Parse(json, EditKind.Pages)!;

        Assert.Contains("\"rotation\":\"Right\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("finish", json, StringComparison.Ordinal);
        Assert.Equal((s_edit, new PageStep(2, PageRotation.Right, true), false), (parsed.EditId, parsed.Steps!.Single(), parsed.IsCommitted));
    }

    [Fact]
    public void ASplit_KeepsItsPartsAndProgress_AndACommitIsRecorded()
    {
        var created = Guid.CreateVersion7();
        var data = new EditData(s_edit)
        {
            Parts = [[new PageStep(1, PageRotation.None, false)], [new PageStep(2, PageRotation.Half, false)]],
            Created = [created],
            Finish = [new FinishMove(FinishMoveKind.Original, created)],
        };

        var parsed = EditDataJson.Parse(EditDataJson.ToJson(data), EditKind.Split)!;

        Assert.Equal(2, parsed.Parts!.Count);
        Assert.Equal([created], parsed.Created);
        Assert.True(parsed.IsCommitted);
        Assert.Equal(FinishMoveKind.Original, parsed.Finish!.Single().Kind);
    }

    [Theory]
    [InlineData("not json", EditKind.Rebuild)]
    [InlineData("{}", EditKind.Rebuild)]
    [InlineData("""{"editId":"0198e3c1-0000-7000-8000-000000000001"}""", EditKind.Pages)]
    [InlineData("""{"editId":"0198e3c1-0000-7000-8000-000000000001","parts":[[{"sourcePage":1}]]}""", EditKind.Split)]
    [InlineData("""{"editId":"0198e3c1-0000-7000-8000-000000000001"}""", EditKind.Append)]
    public void APlanThatDoesNotFitItsKind_CannotBeRead(string json, EditKind kind) =>
        Assert.Null(EditDataJson.Parse(json, kind));

    [Fact]
    public void ARebuild_NeedsOnlyItsId() =>
        Assert.NotNull(EditDataJson.Parse("""{"editId":"0198e3c1-0000-7000-8000-000000000001"}""", EditKind.Rebuild));
}
