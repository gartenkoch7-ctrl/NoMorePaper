using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Application.Security;
using NoMorePaper.Infrastructure.Inputs;
using NoMorePaper.Infrastructure.Security;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Inputs;

/// <summary>Preferences, mail senders and input set-ups in Postgres; what goes with a removed account.</summary>
[Collection(SharedPostgres.Name)]
public sealed class PreferencesStoreTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Preferences_AreSaved_AndReplaced()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "inputs_preferences");
        var admin = await database.AddAdminAsync();

        Assert.Equal(AccountPreferences.Default, await Store(database).GetAsync(admin.Id, Ct));
        Assert.Null(await Store(database).SaveAsync(admin.Id, new AccountPreferences(true, ["b@x.example", "a@x.example"]), Ct));
        Assert.Null(await Store(database).SaveAsync(admin.Id, new AccountPreferences(true, ["c@x.example", "a@x.example"]), Ct));

        var saved = await Store(database).GetAsync(admin.Id, Ct);
        Assert.True(saved.NewDocumentsPrivate);
        Assert.Equal(["a@x.example", "c@x.example"], saved.MailSenders);
        Assert.Equal(admin.Id, await Store(database).FindOwnerOfSenderAsync(" A@X.example ", Ct));
        Assert.Null(await Store(database).FindOwnerOfSenderAsync("b@x.example", Ct));
        Assert.Null(await Store(database).FindOwnerOfSenderAsync("kaputt", Ct));
    }

    [Fact]
    public async Task ASenderOfAnotherAccount_IsRefused_AndNothingChanges()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "inputs_sender_taken");
        var admin = await database.AddAdminAsync();
        var bob = await database.AddMemberAsync(admin.Id, "bob");
        await Store(database).SaveAsync(admin.Id, new AccountPreferences(false, ["papa@x.example"]), Ct);
        await Store(database).SaveAsync(bob.Id, new AccountPreferences(false, ["bob@x.example"]), Ct);

        var taken = await Store(database).SaveAsync(bob.Id, new AccountPreferences(true, ["bob2@x.example", "papa@x.example"]), Ct);

        Assert.Equal("papa@x.example", taken);
        var bobs = await Store(database).GetAsync(bob.Id, Ct);
        Assert.Equal((false, "bob@x.example"), (bobs.NewDocumentsPrivate, Assert.Single(bobs.MailSenders)));
    }

    [Fact]
    public async Task ARemovedAccount_TakesPreferencesSendersSetUpsStateAndSeenItemsWithIt()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "inputs_cascade");
        var admin = await database.AddAdminAsync();
        var bob = await database.AddMemberAsync(admin.Id, "bob");
        var now = DateTimeOffset.UtcNow;
        await Store(database).SaveAsync(bob.Id, new AccountPreferences(true, ["bob@x.example"]), Ct);
        await new EfInputConnections(database.Db()).SaveAsync(new InputConnection(InputKind.ICloud, bob.Id, "{}", [1, 2], now), Ct);
        await new EfInputConnections(database.Db()).SaveAsync(new InputConnection(InputKind.Mail, null, "{}", null, now), Ct);
        await new EfInputStatusStore(database.Db()).RecordAsync(InputKind.ICloud, bob.Id, InputRunResult.Nothing, now, Ct);
        await new EfSeenItems(database.Db()).MarkSeenAsync(InputKind.ICloud, bob.Id, SeenList.Imported, "file-1", now, Ct);

        Assert.True(await database.Store().RemoveAsync(bob.Id, Ct));

        var counts = await database.Db().Database.SqlQuery<string>($"""
            SELECT concat_ws(',', (SELECT count(*) FROM account_preferences), (SELECT count(*) FROM account_mail_senders),
                (SELECT count(*) FROM input_status), (SELECT count(*) FROM input_seen_items),
                (SELECT count(*) FROM input_connections)) AS "Value"
            """).ToListAsync(Ct);

        // Only the household's mailbox is left, which belongs to no account.
        Assert.Equal("0,0,0,0,1", Assert.Single(counts));
        Assert.Null(await Store(database).FindOwnerOfSenderAsync("bob@x.example", Ct));
    }

    [Fact]
    public async Task Connections_AreReplaced_Listed_AndDeleted()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "inputs_connections");
        var admin = await database.AddAdminAsync();
        var store = new EfInputConnections(database.Db());
        var at = new DateTimeOffset(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);

        await store.SaveAsync(new InputConnection(InputKind.Mail, null, """{"host":"a"}""", [1], at), Ct);
        await store.SaveAsync(new InputConnection(InputKind.Mail, null, """{"host":"b"}""", null, at.AddMinutes(1)), Ct);
        await store.SaveAsync(new InputConnection(InputKind.ICloud, admin.Id, "{}", [9], at), Ct);

        var mail = await new EfInputConnections(database.Db()).GetAsync(InputKind.Mail, null, Ct);
        Assert.Equal(("""{"host": "b"}""", null, at.AddMinutes(1)), (mail!.Settings, mail.Secret, mail.UpdatedAt));
        Assert.Equal([admin.Id], (await store.ListAsync(InputKind.ICloud, Ct)).Select(found => found.AccountId!.Value));
        Assert.True(await store.DeleteAsync(InputKind.Mail, null, Ct));
        Assert.False(await store.DeleteAsync(InputKind.Mail, null, Ct));
        Assert.Null(await store.GetAsync(InputKind.Mail, null, Ct));
    }

    [Fact]
    public async Task ASecret_IsNeverStoredInPlainText_AndOpensOnlyForItsPurpose()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "inputs_secret");
        var protector = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider(), NullLogger<DataProtectionSecretProtector>.Instance);
        const string password = "geheimes-postfach-passwort";
        var secret = protector.Protect(SecretPurposes.MailPassword, password);

        await new EfInputConnections(database.Db()).SaveAsync(new InputConnection(InputKind.Mail, null, "{}", secret, DateTimeOffset.UtcNow), Ct);

        var stored = (await database.Db().Database.SqlQuery<byte[]>($"SELECT secret AS \"Value\" FROM input_connections").ToListAsync(Ct)).Single();
        Assert.DoesNotContain(password, Encoding.UTF8.GetString(stored), StringComparison.Ordinal);
        Assert.DoesNotContain(password, Encoding.Latin1.GetString(stored), StringComparison.Ordinal);
        Assert.Equal(password, protector.Unprotect(SecretPurposes.MailPassword, stored));
        Assert.Throws<SecretUnreadableException>(() => protector.Unprotect(SecretPurposes.ICloudSession, stored));
        Assert.Throws<SecretUnreadableException>(() => protector.Unprotect(SecretPurposes.MailPassword, [1, 2, 3]));
    }

    private static EfAccountPreferencesStore Store(AccountDatabase database) => new(database.Db());

}
