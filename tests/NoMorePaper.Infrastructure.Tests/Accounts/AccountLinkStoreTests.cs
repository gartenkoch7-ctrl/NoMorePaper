using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Accounts;

/// <summary>Invitations, reset links and the emergency code: each works once, within its time.</summary>
[Collection(SharedPostgres.Name)]
public sealed class AccountLinkStoreTests(PostgresFixture postgres)
{
    private static readonly string s_invitation = SecretHash.Of("invitation");
    private static readonly string s_reset = SecretHash.Of("reset");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Invitation_IsOpenUntilItExpires_AndWorksOnce_EvenWhenRaced()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "links_invitation");
        var admin = await database.AddAdminAsync();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = new DateTimeOffset(now.Year + 1, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await database.Store().AddInvitationAsync(s_invitation, admin.Id, expiresAt, Ct);

        Assert.Equal(expiresAt, await database.Store().FindOpenInvitationAsync(s_invitation, now, Ct));
        Assert.Null(await database.Store().FindOpenInvitationAsync(s_invitation, expiresAt, Ct));
        Assert.Equal(AccountStatus.NotFound, (await Accept(database, expiresAt, "late")).Status);

        var refused = await database.Store().AcceptInvitationAsync(s_invitation, now, new NewAccount("short", "S", "kurz"), Ct);
        Assert.Equal([AccountProblem.PasswordTooShort], refused.Problems);
        Assert.NotNull(await database.Store().FindOpenInvitationAsync(s_invitation, now, Ct));

        var raced = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => Accept(database, now, $"racer{i}")));
        Assert.Single(raced, result => result.Succeeded);
        Assert.Equal(3, raced.Count(result => result.Status == AccountStatus.NotFound));
        Assert.Null(await database.Store().FindOpenInvitationAsync(s_invitation, now, Ct));
        Assert.Equal(2, await database.Db().Users.CountAsync(Ct));
    }

    [Fact]
    public async Task ResetLink_SetsThePassword_EndsSessionsAndLockout_Once()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "links_reset");
        var admin = await database.AddAdminAsync();
        var member = await database.AddMemberAsync(admin.Id, "forgetful");
        for (var i = 0; i < 5; i++)
        {
            await database.Store().CheckPasswordAsync("forgetful", "wrong-password", Ct);
        }

        var now = DateTimeOffset.UtcNow;
        Assert.False(await database.Store().AddResetTokenAsync(Guid.CreateVersion7(), s_reset, now.AddHours(1), Ct));
        Assert.True(await database.Store().AddResetTokenAsync(member.Id, s_reset, now.AddHours(1), Ct));

        var tooShort = await database.Store().ResetPasswordAsync(s_reset, now, "kurz", Ct);
        var expired = await database.Store().ResetPasswordAsync(s_reset, now.AddHours(2), "new-passphrase", Ct);
        var reset = await database.Store().ResetPasswordAsync(s_reset, now, "new-passphrase", Ct);
        var again = await database.Store().ResetPasswordAsync(s_reset, now, "third-passphrase", Ct);

        Assert.Equal([AccountProblem.PasswordTooShort], tooShort.Problems);
        Assert.Equal(AccountStatus.NotFound, expired.Status);
        Assert.Equal(member.Id, reset.Value);
        Assert.Equal(AccountStatus.NotFound, again.Status);
        Assert.NotEqual(member.SecurityStamp, (await database.Store().FindAsync(member.Id, Ct))!.SecurityStamp);
        Assert.Equal(PasswordCheckStatus.Succeeded, (await database.Store().CheckPasswordAsync("forgetful", "new-passphrase", Ct)).Status);
    }

    [Fact]
    public async Task ResetLink_AppendsAReset_ThatAcknowledgingMarksButNeverDeletes()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "links_reset_notice");
        var admin = await database.AddAdminAsync();
        var member = await database.AddMemberAsync(admin.Id, "told");
        var first = new DateTimeOffset(2026, 11, 18, 20, 15, 30, TimeSpan.Zero);
        var seen = first.AddMinutes(5);
        var second = first.AddDays(1);
        await database.Store().AddResetTokenAsync(member.Id, s_reset, first.AddHours(1), Ct);

        await database.Store().ResetPasswordAsync(s_reset, first, "kurz", Ct);
        var refused = await database.Store().ListAdminResetsAsync(member.Id, Ct);
        await database.Store().ResetPasswordAsync(s_reset, first, "new-passphrase", Ct);
        Assert.True(await database.Store().AcknowledgeAdminResetsAsync(member.Id, seen, Ct));
        await database.Store().AddResetTokenAsync(member.Id, SecretHash.Of("second"), second.AddHours(1), Ct);
        await database.Store().ResetPasswordAsync(SecretHash.Of("second"), second, "third-passphrase", Ct);
        var resets = await database.Store().ListAdminResetsAsync(member.Id, Ct);

        Assert.Empty(refused);
        Assert.Equal([new AdminReset(first, seen), new AdminReset(second, null)], resets);
        Assert.Equal([first, second], AdminResetNotice.From(resets).Resets);
        Assert.False(await database.Store().AcknowledgeAdminResetsAsync(Guid.CreateVersion7(), seen, Ct));
        Assert.Empty(await database.Store().ListAdminResetsAsync(admin.Id, Ct));
    }

    [Fact]
    public async Task EmergencyCode_WorksForTheAdministratorOnly_AndIsReplaced()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "links_emergency");
        var admin = await database.AddAdminAsync();
        await database.AddMemberAsync(admin.Id, "member");
        var store = database.Store();

        var wrong = await store.ResetWithEmergencyCodeAsync("admin", SecretHash.Of("WRONG"), "new-passphrase", SecretHash.Of("NEXT"), Ct);
        var member = await database.Store().ResetWithEmergencyCodeAsync("member", SecretHash.Of("CODE"), "new-passphrase", SecretHash.Of("NEXT"), Ct);
        var unknown = await database.Store().ResetWithEmergencyCodeAsync("nobody", SecretHash.Of("CODE"), "new-passphrase", SecretHash.Of("NEXT"), Ct);
        var tooShort = await database.Store().ResetWithEmergencyCodeAsync("admin", SecretHash.Of("CODE"), "kurz", SecretHash.Of("NEXT"), Ct);
        var reset = await database.Store().ResetWithEmergencyCodeAsync("ADMIN", SecretHash.Of("CODE"), "new-passphrase", SecretHash.Of("NEXT"), Ct);
        var reused = await database.Store().ResetWithEmergencyCodeAsync("admin", SecretHash.Of("CODE"), "other-passphrase", SecretHash.Of("THIRD"), Ct);

        Assert.All([wrong, member, unknown, reused], result => Assert.Equal(AccountStatus.Unauthorized, result.Status));
        Assert.Equal([AccountProblem.PasswordTooShort], tooShort.Problems);
        Assert.Equal(admin.Id, reset.Value);
        var stored = await database.Db().Users.SingleAsync(user => user.Id == admin.Id, Ct);
        Assert.Equal(SecretHash.Of("NEXT"), stored.EmergencyCodeHash);
        Assert.NotEqual(admin.SecurityStamp, stored.SecurityStamp);
        Assert.Equal(PasswordCheckStatus.Succeeded, (await database.Store().CheckPasswordAsync("admin", "new-passphrase", Ct)).Status);
    }

    private static Task<AccountResult<Account>> Accept(AccountDatabase database, DateTimeOffset now, string userName) =>
        database.Store().AcceptInvitationAsync(s_invitation, now, new NewAccount(userName, userName, "lange-passphrase"), Ct);
}
