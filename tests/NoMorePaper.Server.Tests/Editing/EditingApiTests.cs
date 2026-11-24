using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Application.Editing;
using NoMorePaper.Contracts.Editing;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Server.Tests.Classification;
using NoMorePaper.Server.Tests.Documents;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Editing;

/// <summary>The scan editing routes (M4b P1): pages, edit (multipart), append, split, versions.</summary>
public sealed class EditingApiTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static PagePlanDto Plan(params (int Page, bool Replaced)[] steps) =>
        new([.. steps.Select(step => new PageStepDto(step.Page, PageRotationDto.Right, step.Replaced))]);

    [Fact]
    public async Task Pages_AreShownToWhoMaySeeTheDocument()
    {
        var (anna, _) = await api.CreateMemberAsync("edit-pages-anna", Ct);
        var (ben, _) = await api.CreateMemberAsync("edit-pages-ben", Ct);
        var id = await anna.UploadPdfAsync("seiten.pdf", isPrivate: true, Ct);

        var pages = await anna.GetFromJsonAsync<DocumentPagesDto>($"/api/documents/{id}/pages", Ct);

        Assert.Equal((id, "seiten", true, false), (pages!.Id, pages.Title, pages.CanEdit, pages.EditPending));
        Assert.Empty(pages.Pages);
        Assert.Equal(HttpStatusCode.NotFound, (await ben.GetAsync($"/api/documents/{id}/pages", Ct)).StatusCode);
    }

    [Fact]
    public async Task AnEdit_WithACroppedPage_IsAccepted_AndASecondOne_IsAConflict()
    {
        var (anna, _) = await api.CreateMemberAsync("edit-multipart-anna", Ct);
        var id = await ReadyAsync(anna, "bearbeiten.pdf");

        using (var first = await anna.PostAsync($"/api/documents/{id}/edit", Form(Plan((1, true)), CameraPages.PortraitJpeg(), 1), Ct))
        {
            Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        }

        Assert.True((await anna.GetFromJsonAsync<DocumentPagesDto>($"/api/documents/{id}/pages", Ct))!.EditPending);
        Assert.Equal(EditKind.Pages, (await EditAsync(id))!.Kind);
        using var second = await anna.PostAsync($"/api/documents/{id}/edit", Form(Plan((1, false))), Ct);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task AnEdit_IsRefused_ForAnUnprocessedDocument_ABadPlan_OrAStrayPart()
    {
        var (anna, _) = await api.CreateMemberAsync("edit-refused-anna", Ct);
        var queued = await anna.UploadPdfAsync("neu.pdf", isPrivate: false, Ct);
        var id = await ReadyAsync(anna, "fertig.pdf");
        using var badPlan = new MultipartFormDataContent { { new StringContent("""{"steps":[{"sourcePage":1,"rotation":"Sideways"}]}"""), "plan" } };
        var stray = Form(Plan((1, false)));
        stray.Add(new ByteArrayContent(CameraPages.PortraitJpeg()), "image", "x.jpg");

        Assert.Equal(HttpStatusCode.Conflict, (await anna.PostAsync($"/api/documents/{queued}/edit", Form(Plan((1, false))), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anna.PostAsync($"/api/documents/{id}/edit", badPlan, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anna.PostAsync($"/api/documents/{id}/edit", stray, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anna.PostAsync($"/api/documents/{id}/edit", Form(Plan((1, true))), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anna.PostAsJsonAsync($"/api/documents/{id}/edit", Plan((1, false)), Ct)).StatusCode);
        Assert.Null(await EditAsync(id));
    }

    [Fact]
    public async Task Append_IsAccepted_AndRefusedForItself_OrAnotherMembersDocument()
    {
        var (anna, _) = await api.CreateMemberAsync("edit-append-anna", Ct);
        var (ben, _) = await api.CreateMemberAsync("edit-append-ben", Ct);
        var target = await ReadyAsync(anna, "ziel.pdf");
        var appended = await ReadyAsync(anna, "anhang.pdf");
        var foreign = await ReadyAsync(ben, "fremd.pdf", isPrivate: false);

        Assert.Equal(HttpStatusCode.BadRequest, (await anna.PostAsJsonAsync($"/api/documents/{target}/append", new AppendRequestDto(target), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await anna.PostAsJsonAsync($"/api/documents/{target}/append", new AppendRequestDto(foreign), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await anna.PostAsJsonAsync($"/api/documents/{target}/append", new AppendRequestDto(appended), Ct)).StatusCode);
        Assert.Equal(EditKind.Append, (await EditAsync(target))!.Kind);
    }

    [Fact]
    public async Task Split_TakesJson_OrTheMultipartFormWithCroppedPages()
    {
        var (anna, _) = await api.CreateMemberAsync("edit-split-anna", Ct);
        var plain = await ReadyAsync(anna, "stapel.pdf");
        var cropped = await ReadyAsync(anna, "stapel2.pdf");
        var split = new SplitPlanDto([Plan((1, false)), Plan((2, false))]);
        var withCrop = new SplitPlanDto([Plan((1, false)), Plan((2, true))]);
        using var form = new MultipartFormDataContent { { JsonContent.Create(withCrop), "plan" } };
        form.Add(Jpeg(), "page-2", "page-2.jpg");

        Assert.Equal(HttpStatusCode.Accepted, (await anna.PostAsJsonAsync($"/api/documents/{plain}/split", split, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await anna.PostAsync($"/api/documents/{cropped}/split", form, Ct)).StatusCode);
        Assert.Equal(EditKind.Split, (await EditAsync(cropped))!.Kind);
        var single = new SplitPlanDto([Plan((1, false))]);
        Assert.Equal(HttpStatusCode.BadRequest, (await anna.PostAsJsonAsync($"/api/documents/{plain}/split", single, Ct)).StatusCode);
    }

    [Fact]
    public async Task Versions_AreTheOwnersChoice()
    {
        var (anna, _) = await api.CreateMemberAsync("edit-versions-anna", Ct);
        var (ben, benId) = await api.CreateMemberAsync("edit-versions-ben", Ct);
        var id = await ReadyAsync(anna, "fassungen.pdf", isPrivate: false);
        using (var editors = await anna.PutAsJsonAsync($"/api/documents/{id}/editors", new { editorIds = new[] { benId } }, Ct))
        {
            Assert.True(editors.IsSuccessStatusCode);
        }

        using var invalid = new StringContent("""{"policy":"Everything"}""", System.Text.Encoding.UTF8, "application/json");

        Assert.Equal(HttpStatusCode.NoContent, (await anna.PutAsJsonAsync($"/api/documents/{id}/versions", new VersionChoiceDto(VersionPolicyDto.EditedOnly), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ben.PutAsJsonAsync($"/api/documents/{id}/versions", new VersionChoiceDto(VersionPolicyDto.KeepBoth), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anna.PutAsync($"/api/documents/{id}/versions", invalid, Ct)).StatusCode);
    }

    private static MultipartFormDataContent Form(PagePlanDto plan, byte[]? jpeg = null, int page = 0)
    {
        var form = new MultipartFormDataContent { { JsonContent.Create(plan), "plan" } };
        if (jpeg is not null)
        {
            form.Add(Jpeg(jpeg), $"page-{page}", $"page-{page}.jpg");
        }

        return form;
    }

    private static ByteArrayContent Jpeg(byte[]? bytes = null)
    {
        var content = new ByteArrayContent(bytes ?? CameraPages.PortraitJpeg());
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return content;
    }

    // Plays the Worker: processed, and its job gone (OCR and recognition are over).
    private async Task<Guid> ReadyAsync(HttpClient client, string fileName, bool isPrivate = true)
    {
        var id = await client.UploadPdfAsync(fileName, isPrivate, Ct);
        await api.CompleteAsync(id, "Text");
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.ProcessingJobs.Where(job => job.DocumentId == id).ExecuteDeleteAsync(Ct);
        return id;
    }

    private async Task<DocumentEdit?> EditAsync(Guid id)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDocumentEdits>().GetAsync(id, Ct);
    }
}
