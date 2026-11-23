using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.Server.Documents;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Documents;

/// <summary><c>POST /api/documents/pages</c>: camera pages become one PDF document with source "camera".</summary>
public sealed class PageUploadTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TwoJpegs_BecomeOneQueuedPdf_FromTheCamera_NamedAfterThePhoneClock()
    {
        // The phone is in another time zone than the server: its wall clock names the document.
        var takenAt = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(5));

        using var response = await PostAsync([CameraPages.PortraitJpeg(), CameraPages.LandscapeJpeg()],
            takenAt: takenAt.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<UploadResultDto>(Ct))!.Id;
        using var admin = api.CreateAdminClient();
        var detail = await admin.GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{id}", Ct);
        var fields = await admin.GetFromJsonAsync<DocumentFieldsDto>($"/api/documents/{id}/fields", Ct);

        Assert.Equal(DocumentProcessingState.Queued, detail!.State);
        Assert.Equal("application/pdf", detail.Original?.MediaType);
        Assert.Equal($"Kamera {takenAt:yyyy-MM-dd HH-mm}.pdf", detail.Original?.FileName);
        Assert.False(detail.IsPrivate);
        Assert.Equal("camera", fields!.Source);
        Assert.Empty(TempFiles());
    }

    [Fact]
    public async Task PrivateField_MakesTheDocumentPrivate()
    {
        using var response = await PostAsync([CameraPages.PortraitJpeg()], isPrivate: "true");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<UploadResultDto>(Ct))!.Id;
        using var admin = api.CreateAdminClient();
        Assert.True((await admin.GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{id}", Ct))!.IsPrivate);
    }

    [Fact]
    public async Task WithoutPrivateField_TheAccountsSettingDecides_AndAnExplicitChoiceWins()
    {
        var (member, _) = await api.CreateMemberAsync("pages-private-by-account", Ct);
        using var preference = await member.PutAsJsonAsync("/api/account/preferences", new AccountPreferencesDto(true, []), Ct);
        Assert.Equal(HttpStatusCode.NoContent, preference.StatusCode);

        using var unchosen = await PostAsync(member, [CameraPages.PortraitJpeg()]);
        using var shared = await PostAsync(member, [CameraPages.LandscapeJpeg()], isPrivate: "false");

        var (byAccount, appliedByAccount) = (await unchosen.Content.ReadFromJsonAsync<UploadResultDto>(Ct))!;
        var (chosen, appliedChoice) = (await shared.Content.ReadFromJsonAsync<UploadResultDto>(Ct))!;
        Assert.Equal((true, false), (appliedByAccount, appliedChoice));
        Assert.True((await member.GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{byAccount}", Ct))!.IsPrivate);
        Assert.False((await member.GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{chosen}", Ct))!.IsPrivate);
    }

    [Fact]
    public async Task APhoneClockMoreThanADayOff_IsIgnored_ForTheServerTime()
    {
        var before = DateTimeOffset.Now;
        using var response = await PostAsync([CameraPages.PortraitJpeg()], takenAt: "2001-02-03T04:05:06+01:00");

        var id = (await response.Content.ReadFromJsonAsync<UploadResultDto>(Ct))!.Id;
        using var admin = api.CreateAdminClient();
        var name = (await admin.GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{id}", Ct))!.Original!.FileName;
        var named = DateTime.ParseExact(name, "'Kamera 'yyyy-MM-dd HH-mm'.pdf'", CultureInfo.InvariantCulture);
        Assert.InRange(named, before.LocalDateTime.AddMinutes(-1), DateTime.Now.AddMinutes(1));
    }

    [Fact]
    public async Task APng_IsRefused_With400()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3];

        using var response = await PostAsync([CameraPages.PortraitJpeg(), png]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task FiftyOnePages_AreRefused_With400_FiftyAreAccepted()
    {
        var page = CameraPages.PortraitJpeg();

        using var tooMany = await PostAsync([.. Enumerable.Repeat(page, 51)]);
        using var fifty = await PostAsync([.. Enumerable.Repeat(page, 50)]);

        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, fifty.StatusCode);
    }

    [Fact]
    public async Task NoPages_AnEmptyPage_AndABadPrivateValue_AreRefused_With400()
    {
        using var none = await PostAsync([]);
        using var empty = await PostAsync([CameraPages.PortraitJpeg(), []]);
        using var badPrivate = await PostAsync([CameraPages.PortraitJpeg()], isPrivate: "yes");

        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badPrivate.StatusCode);
    }

    [Fact]
    public async Task APageOver15Mb_IsRefused_With413()
    {
        var huge = new byte[(15 * 1024 * 1024) + 1];
        CameraPages.PortraitJpeg().CopyTo(huge, 0);

        using var response = await PostAsync([huge]);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task AJpegThatCannotBeDecoded_IsRefused_With400_AndLeavesNoTemporaryFile()
    {
        byte[] broken = [0xFF, 0xD8, 0xFF, 0xE0, .. Guid.NewGuid().ToByteArray(), 1, 2, 3, 4];

        using var response = await PostAsync([broken]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(TempFiles());
    }

    [Theory]
    [InlineData("2026-11-19T14:30:00+01:00", "Kamera 2026-11-19 14-30.pdf")]
    [InlineData("2026-11-19T14:30:59.123+01:00", "Kamera 2026-11-19 14-30.pdf")]
    [InlineData("2026-11-20T00:15:00+09:00", "Kamera 2026-11-20 00-15.pdf")]
    // Without an offset, in UTC "Z", too far off, or not a time at all: the server's clock.
    [InlineData("2026-11-19T14:30:00", "Kamera 2026-11-19 12-00.pdf")]
    [InlineData("2026-11-19T14:30:00Z", "Kamera 2026-11-19 12-00.pdf")]
    [InlineData("2026-11-21T14:30:00+01:00", "Kamera 2026-11-19 12-00.pdf")]
    [InlineData("gestern", "Kamera 2026-11-19 12-00.pdf")]
    [InlineData(null, "Kamera 2026-11-19 12-00.pdf")]
    public void Name_UsesThePhoneClock_OnlyWithAnOffset_AndWithinADay(string? takenAt, string expected)
    {
        var serverNow = new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.FromHours(1));

        Assert.Equal(expected, PageUploadName.For(takenAt, serverNow));
    }

    private async Task<HttpResponseMessage> PostAsync(byte[][] pages, string? isPrivate = null, string? takenAt = null)
    {
        using var client = api.CreateAdminClient();
        return await PostAsync(client, pages, isPrivate, takenAt);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, byte[][] pages, string? isPrivate = null, string? takenAt = null)
    {
        using var content = new MultipartFormDataContent();
        if (isPrivate is not null)
        {
            content.Add(new StringContent(isPrivate), "private");
        }

        if (takenAt is not null)
        {
            content.Add(new StringContent(takenAt), "takenAt");
        }

        for (var i = 0; i < pages.Length; i++)
        {
            content.Add(new ByteArrayContent(pages[i]), "page", $"seite-{i + 1}.jpg");
        }

        return await client.PostAsync("/api/documents/pages", content, Ct);
    }

    private string[] TempFiles()
    {
        var data = api.Services.GetRequiredService<IConfiguration>()["NoMorePaper:DataPath"]!;
        var folder = Path.Combine(data, PageUpload.TempFolderName);
        return Directory.Exists(folder) ? Directory.GetFiles(folder) : [];
    }
}
