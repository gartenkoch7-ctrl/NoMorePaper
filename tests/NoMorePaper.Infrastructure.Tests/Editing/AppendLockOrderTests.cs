using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Editing;
using NoMorePaper.Infrastructure.Classification;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Persistence;
using NoMorePaper.Infrastructure.Tests.Processing;
using Xunit;
using static NoMorePaper.Infrastructure.Tests.Editing.EditStageData;

namespace NoMorePaper.Infrastructure.Tests.Editing;

/// <summary>
/// Appending locks both documents in the order of their ids, so two requests in opposite directions take
/// turns instead of deadlocking; the second then finds the first's edit pending.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class AppendLockOrderTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TwoAppendsInOppositeDirections_TakeTurns()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "append_lock_order");
        var a = await DoneAsync(data);
        var b = await DoneAsync(data);

        for (var round = 0; round < 3; round++)
        {
            await using var first = data.Context();
            await using var second = data.Context();
            var forward = Append(data, first).AppendAsync(a.Id, b.Id, Ct);
            var backward = Append(data, second).AppendAsync(b.Id, a.Id, Ct);

            var outcomes = await Task.WhenAll(forward, backward).WaitAsync(TimeSpan.FromSeconds(30), Ct);

            Assert.Equal([ChangeOutcome.Changed, ChangeOutcome.Conflict], outcomes.Order());
            await using var reset = data.Context();
            await reset.Database.ExecuteSqlAsync($"DELETE FROM document_edits", Ct);
            await reset.Database.ExecuteSqlAsync($"DELETE FROM processing_jobs", Ct);
        }
    }

    private static AppendDocument Append(QueueDatabase data, Infrastructure.Persistence.AppDbContext db) => new(
        new EfDocumentFieldsStore(db),
        new EfDocumentEdits(db),
        data.Queue(db),
        new FakeCurrentAccount(QueueDatabase.Owner),
        data.Time,
        NullLogger<AppendDocument>.Instance);
}
