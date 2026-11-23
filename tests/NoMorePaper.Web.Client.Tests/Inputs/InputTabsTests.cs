using NoMorePaper.Contracts.Inputs;
using NoMorePaper.Web.Client.Inputs;
using Xunit;

namespace NoMorePaper.Web.Client.Tests.Inputs;

/// <summary>The start tab, the state of a row and the short statement in a tab.</summary>
public sealed class InputTabsTests
{
    private static readonly DateTimeOffset s_now = DateTimeOffset.UtcNow;

    private static InputStatusDto Row(string kind, string? problem = null, bool attention = false, bool ran = true) => new(
        kind, null, problem is null ? "ok" : "problem", ran ? s_now : null, null, problem, attention);

    [Theory]
    [InlineData("mail", "mail")]
    [InlineData("icloud", "icloud")]
    [InlineData("scanner-folder", "scanner-folder")]
    [InlineData("nonsense", "scanner-folder")]
    [InlineData("", "scanner-folder")]
    [InlineData(null, "scanner-folder")]
    public void Initial_TakesAKnownKind_AndOtherwiseTheScannerFolder(string? query, string expected)
    {
        Assert.Equal(expected, InputTabs.Initial(query));
    }

    [Fact]
    public void TabId_PrefixesTheKind() => Assert.Equal("inputs-mail", InputTabs.TabId("mail"));

    [Fact]
    public void StateOf_CoversAllFiveCases()
    {
        Assert.Equal(InputRowState.NeverRun, InputTabs.StateOf(Row("mail", ran: false)));
        Assert.Equal(InputRowState.Running, InputTabs.StateOf(Row("mail")));
        Assert.Equal(InputRowState.NotSetUp, InputTabs.StateOf(Row("mail", "not-configured")));
        Assert.Equal(InputRowState.Problem, InputTabs.StateOf(Row("mail", "unreachable")));
        Assert.Equal(InputRowState.NeedsAttention, InputTabs.StateOf(Row("icloud", "code-needed", attention: true)));
    }

    [Fact]
    public void Summary_CountsOneAndSeveralProblems()
    {
        Assert.Equal(("InpTabProblem", 1), InputTabs.Summary([Row("mail", "unreachable"), Row("mail")], "mail"));
        Assert.Equal(("InpTabProblems", 2), InputTabs.Summary([Row("icloud", "failed"), Row("icloud", "code-needed", true)], "icloud"));
    }

    [Fact]
    public void Summary_WithoutRows_IsNotSetUpForICloudAndNeverElsewhere()
    {
        Assert.Equal(("InpTabNotSetUp", 0), InputTabs.Summary([], "icloud"));
        Assert.Equal(("InpTabNever", 0), InputTabs.Summary([], "mail"));
        Assert.Equal(("InpTabNever", 0), InputTabs.Summary([], "scanner-folder"));
    }

    [Fact]
    public void Summary_NotConfigured_IsNotSetUp_NotAProblem()
    {
        Assert.Equal(("InpTabNotSetUp", 0), InputTabs.Summary([Row("mail", "not-configured")], "mail"));
    }

    [Fact]
    public void Summary_OnlyNeverRun_IsNever_AndMixedWithRunning_IsOk()
    {
        Assert.Equal(("InpTabNever", 0), InputTabs.Summary([Row("icloud", ran: false), Row("icloud", ran: false)], "icloud"));
        Assert.Equal(("InpTabOk", 0), InputTabs.Summary([Row("icloud"), Row("icloud", ran: false)], "icloud"));
    }

    [Fact]
    public void Summary_IgnoresOtherKinds()
    {
        Assert.Equal(("InpTabOk", 0), InputTabs.Summary([Row("mail", "unreachable"), Row("scanner-folder")], "scanner-folder"));
    }

    [Theory]
    [InlineData(InputRowState.NeverRun, "InpBadgeNever")]
    [InlineData(InputRowState.Running, "InpBadgeOk")]
    [InlineData(InputRowState.NotSetUp, "InpBadgeNotSetUp")]
    [InlineData(InputRowState.Problem, "InpBadgeProblem")]
    [InlineData(InputRowState.NeedsAttention, "InpBadgeAttention")]
    public void BadgeKey_NamesTheText(InputRowState state, string key)
    {
        Assert.Equal(key, InputTabs.BadgeKey(state));
        Assert.False(string.IsNullOrEmpty(InputTabs.BadgeIcon(state)));
    }

    [Fact]
    public void KindIcon_IsGivenForEveryKind() => Assert.All(InputTabs.Kinds, kind => Assert.False(string.IsNullOrEmpty(InputTabs.KindIcon(kind))));
}
