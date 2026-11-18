using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Documents;

/// <summary>
/// <c>documents.owner_id</c> stays nullable in the schema (documents received before the setup, ADR-0021),
/// but once the household is set up every document has an owner.
/// </summary>
public sealed class OwnershipTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AfterSetup_NoDocumentIsWithoutOwner_AndEveryUploadBelongsToItsUploader()
    {
        using var admin = api.CreateAdminClient();
        var adminId = (await admin.GetFromJsonAsync<AccountDto>("/api/auth/me", Ct))!.Id;
        var (member, memberId) = await api.CreateMemberAsync("owning-member", Ct);
        var adminUpload = await admin.UploadPdfAsync("vom-verwalter.pdf", isPrivate: false, Ct);
        var memberUpload = await member.UploadPdfAsync("vom-mitglied.pdf", isPrivate: true, Ct);

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owners = await db.Documents.AsNoTracking()
            .Where(document => document.Id == adminUpload || document.Id == memberUpload || document.Id == api.LegacyDocumentId)
            .ToDictionaryAsync(document => document.Id, document => document.OwnerId, Ct);

        Assert.False(await db.Documents.AnyAsync(document => document.OwnerId == null, Ct));
        Assert.Equal(adminId, owners[api.LegacyDocumentId]);
        Assert.Equal(adminId, owners[adminUpload]);
        Assert.Equal(memberId, owners[memberUpload]);
    }
}
