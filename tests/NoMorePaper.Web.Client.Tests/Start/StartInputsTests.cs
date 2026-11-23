using NoMorePaper.Contracts.Inputs;
using NoMorePaper.Web.Client.Inputs;
using NoMorePaper.Web.Client.Start;
using Xunit;

namespace NoMorePaper.Web.Client.Tests.Start;

/// <summary>One line per input on the start page: the worst state, the problems, the order.</summary>
public sealed class StartInputsTests
{
    private static readonly DateTimeOffset s_now = DateTimeOffset.UtcNow;

    private static InputStatusDto Row(string kind, string? problem = null, bool attention = false, bool ran = true, string? account = null) => new(
        kind, account, problem is null ? "ok" : "problem", ran ? s_now : null, null, problem, attention);

    [Fact]
    public void Lines_WithoutRows_HasOneLinePerKind_AndICloudIsNotSetUp()
    {
        var lines = StartInputs.Lines([]);

        Assert.Equal(["scanner-folder", "mail", "icloud"], lines.Select(line => line.Kind));
        Assert.Equal([InputRowState.NeverRun, InputRowState.NeverRun, InputRowState.NotSetUp], lines.Select(line => line.State));
        Assert.All(lines, line => Assert.Empty(line.Problems));
    }

    [Fact]
    public void Lines_PutsTheInputWithAProblemFirst_AndKeepsTheRestInTabOrder()
    {
        var lines = StartInputs.Lines([Row("scanner-folder"), Row("icloud", "sign-in-failed", account: "Ben")]);

        Assert.Equal(["icloud", "scanner-folder", "mail"], lines.Select(line => line.Kind));
        Assert.Equal(InputRowState.Problem, lines[0].State);
        Assert.Equal(InputRowState.Running, lines[1].State);
    }

    [Fact]
    public void Lines_WithTwoICloudAccounts_TakesTheWorstState_AndListsOnlyTheRowWithAProblem()
    {
        var lines = StartInputs.Lines([Row("icloud", account: "Anna"), Row("icloud", "unreachable", account: "Ben")]);

        var icloud = Assert.Single(lines, line => line.Kind == "icloud");
        Assert.Equal(InputRowState.Problem, icloud.State);
        Assert.Equal("Ben", Assert.Single(icloud.Problems).AccountName);
    }

    [Fact]
    public void Lines_NeedsAttentionBeatsAProblem()
    {
        var lines = StartInputs.Lines([Row("icloud", "unreachable", account: "Anna"), Row("icloud", "code-needed", attention: true, account: "Ben")]);

        var icloud = Assert.Single(lines, line => line.Kind == "icloud");
        Assert.Equal(InputRowState.NeedsAttention, icloud.State);
        Assert.Equal(2, icloud.Problems.Count);
    }

    [Fact]
    public void Lines_NotConfigured_IsNotSetUp_AndNoProblem()
    {
        var lines = StartInputs.Lines([Row("mail", "not-configured")]);

        var mail = Assert.Single(lines, line => line.Kind == "mail");
        Assert.Equal(InputRowState.NotSetUp, mail.State);
        Assert.Empty(mail.Problems);
    }

    [Fact]
    public void Lines_IgnoresAnUnknownKind()
    {
        var lines = StartInputs.Lines([Row("fax", "failed")]);

        Assert.Equal(3, lines.Count);
        Assert.All(lines, line => Assert.Empty(line.Problems));
    }
}
