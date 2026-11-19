using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Inputs;

/// <summary>
/// <c>POST /api/documents/{id}/owner-check|hand-over</c>: the owner of a document of unknown origin keeps it
/// or hands it over; others get 404 or 403, a document without check 409.
/// </summary>
public sealed class OwnerCheckApiTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheOwner_SeesTheCheck_AndKeepsTheDocument()
    {
        var (owner, _) = await api.CreateMemberAsync("check-keeper", Ct);
        var id = await UnknownAsync(owner, "unklar.pdf");

        var fields = await owner.GetFromJsonAsync<DocumentFieldsDto>($"/api/documents/{id}/fields", Ct);
        Assert.Equal((true, "upload", "info@unbekannt.example"), (fields!.NeedsOwnerCheck, fields.Source, fields.Mail?.Sender));
        Assert.Contains(await owner.GetFromJsonAsync<ReviewItemDto[]>("/api/review", Ct) ?? [], item => item.Id == id);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/documents/{id}/owner-check", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync($"/api/documents/{id}/owner-check", null, Ct)).StatusCode);
        Assert.False((await owner.GetFromJsonAsync<DocumentFieldsDto>($"/api/documents/{id}/fields", Ct))!.NeedsOwnerCheck);
    }

    [Fact]
    public async Task TheOwner_HandsItOver_ToAMemberWhoThenSeesIt()
    {
        var (owner, _) = await api.CreateMemberAsync("check-giver", Ct);
        var (receiver, receiverId) = await api.CreateMemberAsync("check-receiver", Ct);
        var id = await UnknownAsync(owner, "fuer-dich.pdf");
        Assert.Equal(HttpStatusCode.NotFound, (await receiver.GetAsync($"/api/documents/{id}/fields", Ct)).StatusCode);

        using var handed = await owner.PostAsJsonAsync($"/api/documents/{id}/hand-over", new HandOverRequestDto(receiverId), Ct);

        Assert.Equal(HttpStatusCode.NoContent, handed.StatusCode);
        var fields = await receiver.GetFromJsonAsync<DocumentFieldsDto>($"/api/documents/{id}/fields", Ct);
        Assert.Equal((true, false), (fields!.CanEdit, fields.NeedsOwnerCheck));
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync($"/api/documents/{id}/hand-over", new HandOverRequestDto(receiverId), Ct)).StatusCode);
    }

    [Fact]
    public async Task Refusals_AreNotFound_Invalid_AndConflict()
    {
        var (owner, ownerId) = await api.CreateMemberAsync("check-refuser", Ct);
        var (stranger, _) = await api.CreateMemberAsync("check-stranger", Ct);
        var id = await UnknownAsync(owner, "privat.pdf");
        var plain = await owner.UploadPdfAsync("normal.pdf", isPrivate: false, Ct);

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/api/documents/{id}/owner-check", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/documents/{id}/hand-over", new HandOverRequestDto(Guid.Empty), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/documents/{id}/hand-over", new HandOverRequestDto(ownerId), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/documents/{id}/hand-over", new HandOverRequestDto(Guid.CreateVersion7()), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync($"/api/documents/{plain}/owner-check", null, Ct)).StatusCode);
    }

    /// <summary>An upload turned into a mail of unknown origin, as the mail input will record it.</summary>
    private async Task<Guid> UnknownAsync(HttpClient owner, string fileName)
    {
        var id = await owner.UploadPdfAsync(fileName, isPrivate: false, Ct);
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var document = await db.Documents.FindAsync([id], Ct);
        document!.AttachMailOrigin(new MailOrigin("info@unbekannt.example", "Ihr Vertrag", null));
        document.MarkOwnerCheck();
        await db.SaveChangesAsync(Ct);
        return id;
    }
}
