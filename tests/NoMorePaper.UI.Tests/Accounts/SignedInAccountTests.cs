using System.Text.Json;
using NoMorePaper.Web.Client.Accounts;
using Xunit;

namespace NoMorePaper.UI.Tests.Accounts;

/// <summary>"Who am I" is read with every administrator reset, from the list or the single date.</summary>
public sealed class SignedInAccountTests
{
    private static readonly DateTimeOffset _first = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _later = new(2026, 11, 3, 9, 30, 0, TimeSpan.Zero);

    private const string Account = """
        "id": "00000000-0000-0000-0000-000000000042", "userName": "anna", "displayName": "Anna Beispiel", "isAdmin": false
        """;

    private static SignedInAccount? Read(string extra) =>
        SignedInAccount.Read(JsonDocument.Parse($"{{{Account}{extra}}}").RootElement);

    [Fact]
    public void WithoutResets_TheListIsEmpty()
    {
        var me = Read(string.Empty)!;

        Assert.Equal("anna", me.Account.UserName);
        Assert.Empty(me.AdminResets);
    }

    [Fact]
    public void TheList_IsTakenOldestFirst()
    {
        var me = Read("""
            , "adminResetAt": "2026-11-01T08:00:00+00:00",
            "adminResets": ["2026-11-03T09:30:00+00:00", "2026-11-01T08:00:00+00:00"]
            """)!;

        Assert.Equal(
            new[] { _first, _later },
            me.AdminResets);
    }

    [Theory]
    [InlineData("")]
    [InlineData(""", "adminResets": [] """)]
    [InlineData(""", "adminResets": null """)]
    public void WithoutAList_TheSingleDateIsUsed(string list)
    {
        var me = Read($""", "adminResetAt": "2026-11-01T08:00:00+00:00" {list}""")!;

        Assert.Equal(new[] { _first }, me.AdminResets);
    }

    [Fact]
    public void UnreadableEntries_AreSkipped()
    {
        var me = Read(""", "adminResets": ["gestern", 7, "2026-11-01T08:00:00+00:00"] """)!;

        Assert.Single(me.AdminResets);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("\"anna\"")]
    public void NoAccountObject_IsNull(string json) =>
        Assert.Null(SignedInAccount.Read(JsonDocument.Parse(json).RootElement));
}
