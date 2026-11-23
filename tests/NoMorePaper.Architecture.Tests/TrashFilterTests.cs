using System.Text.RegularExpressions;
using Xunit;

namespace NoMorePaper.Architecture.Tests;

/// <summary>
/// The trash filter of ADR-0025 on the source code: a document in the trash is invisible everywhere.
/// EF queries get it from the named query filter, plain SQL from <c>TrashSql</c>; only the trash itself
/// may look past it.
/// </summary>
/// <remarks>
/// Rule for plain SQL, per file of Infrastructure: every <c>FROM documents</c> and <c>JOIN documents</c>
/// (also across line breaks) needs a trash condition of its own, so their count may not exceed the
/// conditions: uses of <c>TrashSql.NotInTrash</c>/<c>NotInTrashOf</c>, where a constant of the file built
/// from one counts once per use. Not counted are the single-row locks taken before a change
/// (<c>SELECT 1 FROM documents WHERE id = {…} FOR NO KEY UPDATE</c>): they read no columns and are followed
/// by a filtered EF query or by a lock on the job only.
/// </remarks>
public sealed partial class TrashFilterTests
{
    private const string Infrastructure = "NoMorePaper.Infrastructure";

    // Plain SQL that must see documents in the trash on purpose.
    private static readonly HashSet<string> s_seesTrash = new(StringComparer.Ordinal)
    {
        // Merging list entries re-points documents in the trash too, so a restored one keeps a valid entry.
        "EntryMergeSql.cs",
    };

    // Only the trash's own reads and "is it recorded at all" may switch the filter off: file and method.
    private static readonly HashSet<(string File, string Method)> s_ignoresFilter =
    [
        ("EfTrashStore.cs", "FindTrashedDocumentAsync"),
        ("EfTrashStore.cs", "OwnedBy"),
        ("EfTrashStore.cs", "DeleteDocumentAsync"),
        ("EfDocumentRepository.cs", "ExistsAsync"),
    ];

    [Fact]
    public void PlainSqlOverDocuments_HasATrashConditionPerQuery()
    {
        var files = SourceFiles.Production().Where(file => file.Project == Infrastructure).ToList();
        var offenders = files
            .Where(file => !s_seesTrash.Contains(Path.GetFileName(file.Path)))
            .Select(file => (Name: Path.GetFileName(file.Path), Queries: DocumentQueries(file.Text), Conditions: TrashConditions(file.Text)))
            .Where(found => found.Queries > found.Conditions)
            .Select(found => $"{found.Name} ({found.Queries} queries, {found.Conditions} conditions)")
            .ToList();

        Assert.Contains(files, file => DocumentQueries(file.Text) > 1);
        Assert.True(offenders.Count == 0, "Plain SQL over documents without a TrashSql condition each: " + string.Join(", ", offenders));
    }

    [Theory]
    [InlineData("SELECT * FROM documents d WHERE " + "TrashSql.NotInTrash", 1, 1)]
    [InlineData("SELECT 1 FROM documents WHERE id = {id} FOR NO KEY UPDATE", 0, 0)]
    [InlineData("SELECT a FROM\n    documents d JOIN documents e ON true", 2, 0)]
    [InlineData("const string Visible = \" WHERE \" + TrashSql.NotInTrash; x = \"FROM documents d\" + Visible + \"FROM documents d\" + Visible;", 2, 2)]
    public void Counting_FollowsTheRule(string text, int queries, int conditions)
    {
        Assert.Equal(queries, DocumentQueries(text));
        Assert.Equal(conditions, TrashConditions(text));
    }

    [Fact]
    public void OnlyTheTrash_SwitchesTheFilterOff_AndOnlyByName()
    {
        var calls = SourceFiles.Production()
            .SelectMany(file => IgnoreQueryFilters().Matches(file.Text).Select(match =>
                (File: Path.GetFileName(file.Path), Method: EnclosingMethod(file.Text, match.Index), Names: match.Groups["names"].Value)))
            .ToList();
        var offenders = calls
            .Where(call => !s_ignoresFilter.Contains((call.File, call.Method)) || call.Names != "[TrashSql.FilterName]")
            .Select(call => $"{call.File}:{call.Method}")
            .ToList();

        Assert.NotEmpty(calls);
        Assert.True(offenders.Count == 0, "IgnoreQueryFilters outside the trash's methods or without the filter name alone: " + string.Join(", ", offenders));
    }

    private static int DocumentQueries(string text) => DocumentsInQuery().Count(text) - RowLock().Count(text);

    private static int TrashConditions(string text)
    {
        var constants = TrashConstant().Matches(text).Select(match => match.Groups["name"].Value).ToList();
        var usesOfConstants = constants.Sum(name => Regex.Count(text, $@"\b{Regex.Escape(name)}\b") - 1);
        return TrashCondition().Count(text) - constants.Count + usesOfConstants;
    }

    // The last member declared before the call; lambdas and queries inside it have no modifiers.
    private static string EnclosingMethod(string text, int index) =>
        MemberDeclaration().Matches(text[..index]).LastOrDefault()?.Groups["name"].Value ?? string.Empty;

    [GeneratedRegex(@"\b(FROM|JOIN)\s+documents\b")]
    private static partial Regex DocumentsInQuery();

    [GeneratedRegex(@"SELECT\s+1\s+FROM\s+documents\s+WHERE\s+id\s*=\s*\{[^}]*\}\s+FOR\s+NO\s+KEY\s+UPDATE")]
    private static partial Regex RowLock();

    [GeneratedRegex(@"TrashSql\.NotInTrash")]
    private static partial Regex TrashCondition();

    [GeneratedRegex(@"const\s+string\s+(?<name>\w+)\s*=[^;]*TrashSql\.NotInTrash[^;]*;")]
    private static partial Regex TrashConstant();

    [GeneratedRegex(@"^\s*(?:(?:public|private|internal|protected|static|async|override|sealed)\s+)+[\w<>\[\],.? ]+?\s(?<name>\w+)\s*(?:<[^>]*>)?\(", RegexOptions.Multiline)]
    private static partial Regex MemberDeclaration();

    [GeneratedRegex(@"IgnoreQueryFilters\((?<names>[^)]*)\)")]
    private static partial Regex IgnoreQueryFilters();
}
