using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Documents;

public sealed class UploadTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("pdf", "scan.pdf", "application/pdf")]
    [InlineData("jpeg", "foto.JPG", "image/jpeg")]
    [InlineData("png", "bild.png", "image/png")]
    [InlineData("heic", "IMG_0001.HEIC", "image/heic")]
    public async Task Upload_AcceptsAllowedTypes_AndQueuesTheDocument(string kind, string fileName, string mediaType)
    {
        var bytes = kind switch
        {
            "pdf" => SampleFiles.Pdf(),
            "jpeg" => SampleFiles.Jpeg(),
            "png" => SampleFiles.Png(),
            _ => SampleFiles.Heic(),
        };

        using var response = await api.UploadAsync(fileName, bytes, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UploadResultDto>(Ct);
        Assert.NotNull(result);
        Assert.Equal($"/api/documents/{result.Id}", response.Headers.Location?.OriginalString);

        var detail = await api.CreateClient().GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{result.Id}", Ct);
        Assert.NotNull(detail);
        Assert.Equal(DocumentProcessingState.Queued, detail.State);
        Assert.Equal(Path.GetFileNameWithoutExtension(fileName), detail.Title);
        Assert.Equal(mediaType, detail.Original.MediaType);
        Assert.Equal(fileName, detail.Original.FileName);
        Assert.Equal(bytes.Length, detail.Original.SizeBytes);
        Assert.Null(detail.Archive);
    }

    [Fact]
    public async Task Upload_RejectsTextFile_WithProblemDetails415()
    {
        using var response = await api.UploadAsync("notes.pdf", SampleFiles.Text(), Ct);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Upload_IgnoresTheClaimedType_AndLooksAtTheContent()
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(SampleFiles.Text());
        file.Headers.ContentType = new("application/pdf");
        content.Add(file, "file", "fake.pdf");

        using var response = await api.CreateClient().PostAsync("/api/documents", content, Ct);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Upload_RejectsEmptyFile_WithProblemDetails400()
    {
        using var response = await api.UploadAsync("empty.pdf", [], Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Upload_RejectsMissingFileField_AndNonMultipartBodies()
    {
        using var wrongField = new MultipartFormDataContent { { new ByteArrayContent(SampleFiles.Pdf()), "other", "a.pdf" } };
        using var json = JsonContent.Create(new { file = "x" });

        using var first = await api.CreateClient().PostAsync("/api/documents", wrongField, Ct);
        using var second = await api.CreateClient().PostAsync("/api/documents", json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Upload_RejectsFilesOver100Mb_WithProblemDetails413()
    {
        var tooLarge = SampleFiles.WithHead("%PDF-"u8, totalLength: (101 * 1024 * 1024));

        using var response = await api.UploadAsync("big.pdf", tooLarge, Ct);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Upload_AcceptsAFileJustUnder100Mb()
    {
        var large = SampleFiles.WithHead("%PDF-"u8, totalLength: (100 * 1024 * 1024) - 1);

        using var response = await api.UploadAsync("large.pdf", large, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Upload_KeepsOnlyTheFileNamePart_OfAPathLikeName()
    {
        using var response = await api.UploadAsync("..\\..\\etc/evil name.pdf", SampleFiles.Pdf(), Ct);
        var result = await response.Content.ReadFromJsonAsync<UploadResultDto>(Ct);

        var detail = await api.CreateClient().GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{result!.Id}", Ct);

        Assert.Equal("evil name.pdf", detail!.Original.FileName);
        Assert.Equal("evil name", detail.Title);
    }

    [Fact]
    public async Task Download_ReturnsTheOriginalUnchanged_AndSupportsRanges()
    {
        var bytes = SampleFiles.WithHead("%PDF-1.4 "u8, totalLength: 300_000);
        using var upload = await api.UploadAsync("brief.pdf", bytes, Ct);
        var id = (await upload.Content.ReadFromJsonAsync<UploadResultDto>(Ct))!.Id;
        var client = api.CreateClient();

        using var response = await client.GetAsync($"/api/documents/{id}/files/original", Ct);
        var downloaded = await response.Content.ReadAsByteArrayAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("brief.pdf", response.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(downloaded));

        var detail = await client.GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{id}", Ct);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), detail!.Original.Sha256);

        using var range = new HttpRequestMessage(HttpMethod.Get, $"/api/documents/{id}/files/original");
        range.Headers.Range = new(0, 99);
        using var partial = await client.SendAsync(range, Ct);
        Assert.Equal(HttpStatusCode.PartialContent, partial.StatusCode);
        Assert.Equal(bytes[..100], await partial.Content.ReadAsByteArrayAsync(Ct));
    }
}
