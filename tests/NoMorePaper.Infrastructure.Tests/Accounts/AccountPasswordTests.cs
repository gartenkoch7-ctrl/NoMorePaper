using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Accounts;

/// <summary>Password changes and races around new accounts.</summary>
[Collection(SharedPostgres.Name)]
public sealed class AccountPasswordTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ChangePassword_WrongCurrentPasswords_CountTowardsTheLockout()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "passwords_change_lockout");
        var admin = await database.AddAdminAsync();
        var member = await database.AddMemberAsync(admin.Id, "stolen-session");

        for (var i = 0; i < 4; i++)
        {
            var wrong = await database.Store().ChangePasswordAsync(member.Id, "guess-number-" + i, "new-passphrase", Ct);
            Assert.Equal([AccountProblem.CurrentPasswordWrong], wrong.Problems);
        }

        // Like at sign-in, the guess that locks the account is answered with the lockout.
        var fifth = await database.Store().ChangePasswordAsync(member.Id, "guess-number-5", "new-passphrase", Ct);
        var right = await database.Store().ChangePasswordAsync(member.Id, "member-passphrase", "new-passphrase", Ct);
        var signIn = await database.Store().CheckPasswordAsync("stolen-session", "member-passphrase", Ct);

        Assert.Equal(AccountStatus.LockedOut, fifth.Status);
        Assert.Equal(AccountStatus.LockedOut, right.Status);
        Assert.Empty(right.Problems);
        Assert.InRange(right.LockedUntil!.Value - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(4), AccountRules.LockoutDuration);
        Assert.Equal(PasswordCheckStatus.LockedOut, signIn.Status);
    }

    [Fact]
    public async Task PasswordChanges_VoidEveryOpenResetLink()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "passwords_void_links");
        var admin = await database.AddAdminAsync();
        var member = await database.AddMemberAsync(admin.Id, "two-links");
        var later = DateTimeOffset.UtcNow.AddHours(1);
        await database.Store().AddResetTokenAsync(member.Id, SecretHash.Of("first"), later, Ct);
        await database.Store().AddResetTokenAsync(member.Id, SecretHash.Of("second"), later, Ct);
        await database.Store().AddResetTokenAsync(member.Id, SecretHash.Of("third"), later, Ct);

        var reset = await database.Store().ResetPasswordAsync(SecretHash.Of("first"), DateTimeOffset.UtcNow, "reset-passphrase", Ct);
        var second = await database.Store().ResetPasswordAsync(SecretHash.Of("second"), DateTimeOffset.UtcNow, "other-passphrase", Ct);
        await database.Store().AddResetTokenAsync(member.Id, SecretHash.Of("fourth"), later, Ct);
        var changed = await database.Store().ChangePasswordAsync(member.Id, "reset-passphrase", "changed-passphrase", Ct);

        Assert.True(reset.Succeeded);
        Assert.Equal(AccountStatus.NotFound, second.Status);
        Assert.True(changed.Succeeded);
        Assert.Empty(await database.Db().PasswordResetTokens.ToListAsync(Ct));
    }

    [Fact]
    public async Task AcceptingTwoInvitations_WithTheSameNameAtOnce_RefusesOneAsTaken()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "passwords_same_name");
        var admin = await database.AddAdminAsync();
        var hashes = Enumerable.Range(0, 4).Select(i => SecretHash.Of("invite-" + i)).ToList();
        foreach (var hash in hashes)
        {
            await database.Store().AddInvitationAsync(hash, admin.Id, DateTimeOffset.UtcNow.AddDays(1), Ct);
        }

        var results = await Task.WhenAll(hashes.Select(hash => database.Store().AcceptInvitationAsync(
            hash, DateTimeOffset.UtcNow, new NewAccount("twin", "Twin", "lange-passphrase"), Ct)));

        Assert.Single(results, result => result.Succeeded);
        Assert.All(results.Where(result => !result.Succeeded), result => Assert.Equal([AccountProblem.UserNameTaken], result.Problems));
    }
}
