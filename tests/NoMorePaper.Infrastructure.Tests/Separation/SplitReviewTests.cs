using NoMorePaper.Domain.Editing;
using NoMorePaper.Infrastructure.Classification;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Classification;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Separation;

/// <summary>A split suggestion brings its stack into "to review" for everybody who may edit it, and nobody else.</summary>
[Collection(SharedPostgres.Name)]
public sealed class SplitReviewTests(PostgresFixture postgres)
{
    private static readonly SplitSuggestion s_suggestion = new([new PageRange(1, 2), new PageRange(3, 3)], [2], [SplitReason.Blank]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Review_ListsTheStack_WithItsSuggestion_ForOwnerAndEditors()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "review_split");
        var admin = await database.AddAdminAsync();
        var bob = await database.AddMemberAsync(admin.Id, "bob");
        var carol = await database.AddMemberAsync(admin.Id, "carol");
        var db = database.Db();
        var stack = await Seed.DocumentAsync(db, admin.Id, isPrivate: false, text: "Sehr geehrter Herr Mustermann");
        var plain = await Seed.DocumentAsync(db, admin.Id, isPrivate: false, text: "Rechnung");
        stack.SuggestSplit(s_suggestion);
        stack.SetEditors(admin.Id, [bob.Id]);
        await db.SaveChangesAsync(Ct);

        var forAdmin = await new EfDocumentFieldsStore(database.Db()).ListReviewAsync(admin.Id, Ct);
        var forBob = await new EfDocumentFieldsStore(database.Db()).ListReviewAsync(bob.Id, Ct);
        var forCarol = await new EfDocumentFieldsStore(database.Db()).ListReviewAsync(carol.Id, Ct);

        var item = Assert.Single(forAdmin);
        Assert.Equal(stack.Id, item.Id);
        Assert.NotNull(item.Split);
        Assert.Equal(s_suggestion.Parts, item.Split.Parts);
        Assert.Equal([2], item.Split.BlankPages);
        Assert.Equal([stack.Id], forBob.Select(found => found.Id));
        Assert.Empty(forCarol);
        Assert.DoesNotContain(forAdmin, found => found.Id == plain.Id);
        Assert.Equal(1, await new EfDocumentFieldsStore(database.Db()).CountReviewAsync(admin.Id, Ct));
    }

    [Fact]
    public async Task Review_DropsTheStack_OnceTheSuggestionIsAnswered()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "review_split_answered");
        var admin = await database.AddAdminAsync();
        var db = database.Db();
        var stack = await Seed.DocumentAsync(db, admin.Id, isPrivate: false, text: "Sehr geehrter Herr Mustermann");
        stack.SuggestSplit(s_suggestion);
        await db.SaveChangesAsync(Ct);
        stack.DismissSplit();
        await db.SaveChangesAsync(Ct);

        Assert.Empty(await new EfDocumentFieldsStore(database.Db()).ListReviewAsync(admin.Id, Ct));
        Assert.Equal(0, await new EfDocumentFieldsStore(database.Db()).CountReviewAsync(admin.Id, Ct));
    }
}
