using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Infrastructure.Inputs;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Inputs;

/// <summary>The state of the inputs and the items they have seen, in Postgres.</summary>
[Collection(SharedPostgres.Name)]
public sealed class InputStatusStoreTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset s_at = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Status_KeepsTheLastSuccess_TheLatestProblem_AndCountsDocuments()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "inputs_status");
        var admin = await database.AddAdminAsync();
        var store = new EfInputStatusStore(database.Db());

        await store.RecordAsync(InputKind.Mail, null, new InputRunResult(2, 1), s_at, Ct);
        await store.RecordAsync(InputKind.Mail, null, new InputRunResult(1, 0, new InputProblem(InputProblemKind.LlmUnavailable)), s_at.AddMinutes(5), Ct);
        await store.RecordAsync(InputKind.ICloud, admin.Id, InputRunResult.Failed(new InputProblem(InputProblemKind.CodeNeeded, admin.Id, NeedsAttention: true)), s_at, Ct);

        var rows = await new EfInputStatusStore(database.Db()).ListAsync(Ct);
        var mail = rows.Single(row => row.Kind == InputKind.Mail);
        Assert.Equal(
            new InputStatus(InputKind.Mail, null, s_at.AddMinutes(5), s_at, InputProblemKind.LlmUnavailable, false, 3),
            mail);
        var icloud = rows.Single(row => row.Kind == InputKind.ICloud);
        Assert.Equal((admin.Id, (DateTimeOffset?)null, InputProblemKind.CodeNeeded, true), (icloud.AccountId!.Value, icloud.LastSuccessAt, icloud.Problem!.Value, icloud.NeedsAttention));

        await store.RecordAsync(InputKind.Mail, null, InputRunResult.Nothing, s_at.AddMinutes(10), Ct);
        var cleared = (await store.ListAsync(Ct)).Single(row => row.Kind == InputKind.Mail);
        Assert.Equal((null, s_at.AddMinutes(10)), (cleared.Problem, cleared.LastSuccessAt!.Value));
    }

    [Fact]
    public async Task SeenItems_AreRemembered_PerInputAccountAndList_ByHashOnly()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "inputs_seen");
        var admin = await database.AddAdminAsync();
        var seen = new EfSeenItems(database.Db());
        const string messageId = "<rechnung-4711@stadtwerke.example>";

        await seen.MarkSeenAsync(InputKind.Mail, null, SeenList.Spam, messageId, s_at, Ct);
        await seen.MarkSeenAsync(InputKind.Mail, null, SeenList.Spam, messageId, s_at, Ct);

        Assert.True(await seen.HasSeenAsync(InputKind.Mail, null, SeenList.Spam, messageId, Ct));
        Assert.False(await seen.HasSeenAsync(InputKind.Mail, null, SeenList.Imported, messageId, Ct));
        Assert.False(await seen.HasSeenAsync(InputKind.Mail, admin.Id, SeenList.Spam, messageId, Ct));
        Assert.False(await seen.HasSeenAsync(InputKind.ICloud, null, SeenList.Spam, messageId, Ct));

        var hashes = await database.Db().Database.SqlQuery<string>($"SELECT key_hash AS \"Value\" FROM input_seen_items").ToListAsync(Ct);
        Assert.Equal(64, Assert.Single(hashes).Length);
        Assert.DoesNotContain("stadtwerke", hashes[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failures_AreCounted_AndForgotten()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "inputs_failures");
        var seen = new EfSeenItems(database.Db());

        Assert.Equal(1, await seen.IncrementFailuresAsync(InputKind.Mail, null, "m-1", s_at, Ct));
        Assert.Equal(2, await seen.IncrementFailuresAsync(InputKind.Mail, null, "m-1", s_at, Ct));
        Assert.Equal(1, await seen.IncrementFailuresAsync(InputKind.Mail, null, "m-2", s_at, Ct));
        Assert.True(await seen.HasSeenAsync(InputKind.Mail, null, SeenList.Failed, "m-1", Ct));

        await seen.ForgetAsync(InputKind.Mail, null, SeenList.Failed, "m-1", Ct);

        Assert.False(await seen.HasSeenAsync(InputKind.Mail, null, SeenList.Failed, "m-1", Ct));
        Assert.Equal(1, await seen.IncrementFailuresAsync(InputKind.Mail, null, "m-1", s_at, Ct));
    }
}
