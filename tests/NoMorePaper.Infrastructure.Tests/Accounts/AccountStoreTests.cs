using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Accounts;

/// <summary>Setup, sign-in with lockout, sessions and removal against Postgres.</summary>
[Collection(SharedPostgres.Name)]
public sealed class AccountStoreTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SetUp_CreatesTheAdministrator_AndAdoptsDocumentsWithoutOwner_Once()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "accounts_setup");
        var legacy = await database.AddDocumentWithoutOwnerAsync();

        var first = await database.Store().SetUpAsync(new NewAccount("anna", "Anna", "lange-passphrase"), SecretHash.Of("CODE"), Ct);
        var second = await database.Store().SetUpAsync(new NewAccount("bob", "Bob", "lange-passphrase"), SecretHash.Of("CODE"), Ct);

        var admin = first.Value!.Administrator;
        Assert.Equal((1, true, "anna"), (first.Value.AdoptedDocuments, admin.IsAdmin, admin.UserName));
        Assert.Equal(AccountStatus.Conflict, second.Status);
        var document = await database.Db().Documents.SingleAsync(d => d.Id == legacy, Ct);
        Assert.Equal((admin.Id, false), (document.OwnerId, document.IsPrivate));
        var stored = await database.Db().Users.SingleAsync(Ct);
        Assert.Equal(SecretHash.Of("CODE"), stored.EmergencyCodeHash);
        Assert.DoesNotContain("lange-passphrase", stored.PasswordHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetUp_RacingSetups_CreateExactlyOneAdministrator()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "accounts_setup_race");

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(i =>
            database.Store().SetUpAsync(new NewAccount($"admin{i}", "Admin", "lange-passphrase"), SecretHash.Of("CODE"), Ct)));

        Assert.Single(results, result => result.Succeeded);
        Assert.Equal(4, results.Count(result => result.Status == AccountStatus.Conflict));
        Assert.Equal(1, await database.Db().Users.CountAsync(Ct));
    }

    [Fact]
    public async Task Passwords_NeedTenCharacters_AndNothingElse_AndNamesAreUniqueIgnoringCase()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "accounts_rules");
        var admin = await database.AddAdminAsync();
        var store = database.Store();

        var tooShort = await store.AcceptInvitationAsync("x", DateTimeOffset.UtcNow, new NewAccount("a", "A", "123456789"), Ct);
        var member = await database.AddMemberAsync(admin.Id, "plain", "aaaaaaaaaa");
        var duplicate = await Accept(database, admin.Id, new NewAccount("PLAIN", "P", "lange-passphrase"));
        var shortViaInvite = await Accept(database, admin.Id, new NewAccount("other", "O", "123456789"));

        Assert.Equal(AccountStatus.NotFound, tooShort.Status);
        Assert.Equal("plain", member.UserName);
        Assert.Equal([AccountProblem.UserNameTaken], duplicate.Problems);
        Assert.Equal([AccountProblem.PasswordTooShort], shortViaInvite.Problems);
    }

    [Fact]
    public async Task CheckPassword_LocksAfterFiveFailures_ForFiveMinutes_EvenForTheRightPassword()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "accounts_lockout");
        var admin = await database.AddAdminAsync();
        await database.AddMemberAsync(admin.Id, "guessed");

        var unknown = await database.Store().CheckPasswordAsync("nobody", "whatever-123", Ct);
        var failures = new List<PasswordCheck>();
        for (var i = 0; i < 5; i++)
        {
            failures.Add(await database.Store().CheckPasswordAsync("guessed", "wrong-password", Ct));
        }

        var right = await database.Store().CheckPasswordAsync("GUESSED", "member-passphrase", Ct);

        Assert.Equal((PasswordCheckStatus.Failed, null), (unknown.Status, unknown.AccountId));
        Assert.All(failures.Take(4), check => Assert.Equal(PasswordCheckStatus.Failed, check.Status));
        Assert.Equal(PasswordCheckStatus.LockedOut, failures[4].Status);
        Assert.Equal(PasswordCheckStatus.LockedOut, right.Status);
        Assert.InRange(right.LockedUntil!.Value - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(4.9), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task CheckPassword_Success_ResetsTheFailureCount()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "accounts_reset_count");
        var admin = await database.AddAdminAsync();
        await database.AddMemberAsync(admin.Id, "careless");

        for (var i = 0; i < 4; i++)
        {
            await database.Store().CheckPasswordAsync("careless", "wrong-password", Ct);
        }

        var success = await database.Store().CheckPasswordAsync("careless", "member-passphrase", Ct);
        var afterwards = await database.Store().CheckPasswordAsync("careless", "wrong-password", Ct);

        Assert.Equal(PasswordCheckStatus.Succeeded, success.Status);
        Assert.Equal("careless", success.Account!.UserName);
        Assert.Equal(PasswordCheckStatus.Failed, afterwards.Status);
    }

    [Fact]
    public async Task StampAndPassword_ChangesEndSessions_AndRemovalTakesTheLinksAlong()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "accounts_sessions");
        var admin = await database.AddAdminAsync();
        var member = await database.AddMemberAsync(admin.Id, "leaving");
        await database.Store().AddResetTokenAsync(member.Id, SecretHash.Of("reset"), DateTimeOffset.UtcNow.AddHours(1), Ct);

        Assert.True(await database.Store().RenewSecurityStampAsync(member.Id, Ct));
        var renewed = await database.Store().FindAsync(member.Id, Ct);
        var wrong = await database.Store().ChangePasswordAsync(member.Id, "not-it-at-all", "new-passphrase", Ct);
        var changed = await database.Store().ChangePasswordAsync(member.Id, "member-passphrase", "new-passphrase", Ct);

        Assert.NotEqual(member.SecurityStamp, renewed!.SecurityStamp);
        Assert.Equal([AccountProblem.CurrentPasswordWrong], wrong.Problems);
        Assert.NotEqual(renewed.SecurityStamp, changed.Value!.SecurityStamp);
        Assert.True(await database.Store().ExistsAsync(member.Id, Ct));
        Assert.True(await database.Store().RemoveAsync(member.Id, Ct));
        Assert.False(await database.Store().RemoveAsync(member.Id, Ct));
        Assert.Null(await database.Store().FindAsync(member.Id, Ct));
        Assert.False(await database.Store().ExistsAsync(member.Id, Ct));
        Assert.Equal(AccountStatus.NotFound, (await database.Store().ChangePasswordAsync(member.Id, "new-passphrase", "x-passphrase", Ct)).Status);
        Assert.Empty(await database.Db().PasswordResetTokens.ToListAsync(Ct));
        Assert.False(await database.Store().RenewSecurityStampAsync(member.Id, Ct));
        Assert.Equal([admin.Id], (await database.Store().ListAsync(Ct)).Select(account => account.Id));
    }

    private static async Task<AccountResult<Account>> Accept(AccountDatabase database, Guid adminId, NewAccount member)
    {
        var tokenHash = SecretHash.Of(Guid.NewGuid().ToString());
        await database.Store().AddInvitationAsync(tokenHash, adminId, DateTimeOffset.UtcNow.AddDays(1), Ct);
        return await database.Store().AcceptInvitationAsync(tokenHash, DateTimeOffset.UtcNow, member, Ct);
    }
}
