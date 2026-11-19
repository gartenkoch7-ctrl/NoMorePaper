using System.Net;
using System.Net.Http.Json;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Classification;

/// <summary>
/// <c>/api/documents/{id}/fields|confirm|editors</c>: the owner and chosen editors change fields, others
/// get 403, and another account's private document is 404 everywhere.
/// </summary>
public sealed class FieldsApiTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheOwner_EditsAndConfirms_AndSeesTheFields()
    {
        var (owner, _) = await api.CreateMemberAsync("fields-owner", Ct);
        var id = await owner.UploadPdfAsync("rechnung.pdf", isPrivate: false, Ct);
        var sender = await owner.CreateEntryAsync("correspondents", "Stadtwerke München", Ct);
        var tag = await owner.CreateEntryAsync("tags", "Strom", Ct);
        await api.ClassifyAsync(id, Recognition.Fields());

        var recognized = await owner.GetFromJsonAsync<DocumentFieldsDto>($"/api/documents/{id}/fields", Ct);
        Assert.Equal((DocumentRecognitionState.Automatic, true), (recognized!.Recognition, recognized.CanEdit));

        var edit = new EditFieldsRequestDto("Stromrechnung", sender, null, new DateOnly(2026, 11, 1), new MoneyDto(84.20m, "EUR"), "Abschlag.", [tag]);
        using var put = await owner.PutAsJsonAsync($"/api/documents/{id}/fields", edit, Ct);
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var fields = await owner.GetFromJsonAsync<DocumentFieldsDto>($"/api/documents/{id}/fields", Ct);
        Assert.Equal(("Stadtwerke München", "Strom", DocumentRecognitionState.Confirmed), (fields!.Correspondent?.Name, Assert.Single(fields.Tags).Name, fields.Recognition));
        Assert.Equal(new MoneyDto(84.20m, "EUR"), fields.Amount);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/documents/{id}/confirm", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task AnotherMember_SeesSharedFields_ButMayNotChangeThem_UntilChosenAsEditor()
    {
        var (owner, _) = await api.CreateMemberAsync("fields-sharer", Ct);
        var (other, otherId) = await api.CreateMemberAsync("fields-helper", Ct);
        var id = await owner.UploadPdfAsync("geteilt.pdf", isPrivate: false, Ct);
        var edit = new EditFieldsRequestDto(null, null, null, null, null, null, []);

        Assert.False((await other.GetFromJsonAsync<DocumentFieldsDto>($"/api/documents/{id}/fields", Ct))!.CanEdit);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync($"/api/documents/{id}/fields", edit, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync($"/api/documents/{id}/confirm", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync($"/api/documents/{id}/editors", new SetEditorsRequestDto([otherId]), Ct)).StatusCode);

        using var chosen = await owner.PutAsJsonAsync($"/api/documents/{id}/editors", new SetEditorsRequestDto([otherId]), Ct);
        Assert.Equal(HttpStatusCode.NoContent, chosen.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await other.PutAsJsonAsync($"/api/documents/{id}/fields", edit, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync($"/api/documents/{id}/editors", new SetEditorsRequestDto([]), Ct)).StatusCode);
        Assert.Equal([otherId], (await owner.GetFromJsonAsync<DocumentFieldsDto>($"/api/documents/{id}/fields", Ct))!.EditorIds);
    }

    [Fact]
    public async Task AnotherAccountsPrivateDocument_IsNotFound_ForEveryFieldRoute()
    {
        var (owner, _) = await api.CreateMemberAsync("fields-private", Ct);
        var id = await owner.UploadPdfAsync("geheim.pdf", isPrivate: true, Ct);
        using var stranger = api.CreateAdminClient();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/documents/{id}/fields", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsJsonAsync($"/api/documents/{id}/fields", new EditFieldsRequestDto(null, null, null, null, null, null, []), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/api/documents/{id}/confirm", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsJsonAsync($"/api/documents/{id}/editors", new SetEditorsRequestDto([]), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/api/documents/{id}/duplicate/keep", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/documents/{id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task InvalidFields_AndEntriesTheEditorMayNotSee_AreRefusedWith400()
    {
        var (owner, _) = await api.CreateMemberAsync("fields-invalid", Ct);
        var (other, _) = await api.CreateMemberAsync("fields-secret", Ct);
        var id = await owner.UploadPdfAsync("ungueltig.pdf", isPrivate: false, Ct);
        var secret = await other.CreateEntryAsync("correspondents", "Psychotherapie Dr. Weiß", Ct);
        var empty = new EditFieldsRequestDto(null, null, null, null, null, null, []);

        using var hidden = await owner.PutAsJsonAsync($"/api/documents/{id}/fields", empty with { CorrespondentId = secret }, Ct);
        using var currency = await owner.PutAsJsonAsync($"/api/documents/{id}/fields", empty with { Amount = new MoneyDto(1m, "euro") }, Ct);
        using var noTags = await owner.PutAsync($"/api/documents/{id}/fields", JsonContent.Create(new { title = "x" }), Ct);
        using var unknownEditor = await owner.PutAsJsonAsync($"/api/documents/{id}/editors", new SetEditorsRequestDto([Guid.CreateVersion7()]), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, currency.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noTags.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownEditor.StatusCode);
        Assert.Null((await owner.GetFromJsonAsync<DocumentFieldsDto>($"/api/documents/{id}/fields", Ct))!.Correspondent);
    }

    [Fact]
    public async Task Household_ListsMembersForEverybody_WithoutUserNames()
    {
        var (member, memberId) = await api.CreateMemberAsync("household-member", Ct);

        using var response = await member.GetAsync("/api/household", Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(memberId.ToString(), body, StringComparison.Ordinal);
        Assert.Contains("household-member Name", body, StringComparison.Ordinal);
        Assert.DoesNotContain("userName", body, StringComparison.OrdinalIgnoreCase);
    }
}
