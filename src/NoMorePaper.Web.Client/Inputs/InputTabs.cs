using MudBlazor;
using NoMorePaper.Contracts.Inputs;

namespace NoMorePaper.Web.Client.Inputs;

/// <summary>Tab ids, the start tab, the state of a row and the short statement in the tab. Pure logic.</summary>
public static class InputTabs
{
    private const string NotConfigured = "not-configured";

    /// <summary>The inputs in tab order; the first one is also the start tab without a valid <c>?quelle=</c>.</summary>
    public static IReadOnlyList<string> Kinds { get; } = ["scanner-folder", "mail", "icloud"];

    public static string TabId(string kind) => "inputs-" + kind;

    /// <summary>The kind to start on: the query value if it is a known input, otherwise the scanner folder.</summary>
    public static string Initial(string? query) => query is not null && Kinds.Contains(query) ? query : Kinds[0];

    public static InputRowState StateOf(InputStatusDto row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.LastRunAt is null)
        {
            return InputRowState.NeverRun;
        }

        if (row.Problem == NotConfigured)
        {
            return InputRowState.NotSetUp;
        }

        if (row.Problem is not null)
        {
            return row.NeedsAttention ? InputRowState.NeedsAttention : InputRowState.Problem;
        }

        return InputRowState.Running;
    }

    /// <summary>
    /// The short statement for the tab of <paramref name="kind"/>: the key of the text and the number of rows with a
    /// problem. Without a problem the least settled state wins, so "running" means that nothing is open.
    /// </summary>
    public static (string Key, int Problems) Summary(IEnumerable<InputStatusDto> rows, string kind)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var states = rows.Where(row => row.Kind == kind).Select(StateOf).ToArray();
        var problems = states.Count(state => state is InputRowState.Problem or InputRowState.NeedsAttention);
        if (problems > 0)
        {
            return (problems == 1 ? "InpTabProblem" : "InpTabProblems", problems);
        }

        if (states.Length == 0)
        {
            return (kind == "icloud" ? "InpTabNotSetUp" : "InpTabNever", 0);
        }

        if (states.Contains(InputRowState.NotSetUp))
        {
            return ("InpTabNotSetUp", 0);
        }

        return (states.All(state => state == InputRowState.NeverRun) ? "InpTabNever" : "InpTabOk", 0);
    }

    public static string BadgeKey(InputRowState state) => state switch
    {
        InputRowState.NeverRun => "InpBadgeNever",
        InputRowState.Running => "InpBadgeOk",
        InputRowState.NotSetUp => "InpBadgeNotSetUp",
        InputRowState.Problem => "InpBadgeProblem",
        _ => "InpBadgeAttention",
    };

    public static string BadgeIcon(InputRowState state) => state switch
    {
        InputRowState.NeverRun => Icons.Material.Filled.Schedule,
        InputRowState.Running => Icons.Material.Filled.CheckCircle,
        InputRowState.NotSetUp => Icons.Material.Filled.Info,
        _ => Icons.Material.Filled.Error,
    };

    public static string KindIcon(string kind) => kind switch
    {
        "mail" => Icons.Material.Outlined.Mail,
        "icloud" => Icons.Material.Outlined.Cloud,
        _ => Icons.Material.Outlined.FolderShared,
    };
}
