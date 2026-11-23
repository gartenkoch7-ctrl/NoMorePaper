using NoMorePaper.Contracts.Inputs;
using NoMorePaper.Web.Client.Inputs;

namespace NoMorePaper.Web.Client.Start;

/// <summary>Condenses the rows of <c>GET /api/inputs</c> into one line per input for the start page. Pure logic.</summary>
public static class StartInputs
{
    public static IReadOnlyList<StartInputLine> Lines(IEnumerable<InputStatusDto> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var all = rows.ToArray();
        var lines = InputTabs.Kinds.Select(kind => LineOf(kind, all.Where(row => row.Kind == kind).ToArray()));

        // OrderBy is stable: lines without a problem keep the order of the tabs.
        return [.. lines.OrderBy(line => line.Problems.Count > 0 ? 0 : 1)];
    }

    private static StartInputLine LineOf(string kind, InputStatusDto[] rows)
    {
        if (rows.Length == 0)
        {
            return new StartInputLine(kind, kind == "icloud" ? InputRowState.NotSetUp : InputRowState.NeverRun, []);
        }

        var problems = rows.Where(row => InputTabs.StateOf(row) is InputRowState.Problem or InputRowState.NeedsAttention).ToArray();

        // The worst row speaks for the input, so one broken iCloud account is not hidden by a working one.
        return new StartInputLine(kind, rows.Select(InputTabs.StateOf).MaxBy(Rank), problems);
    }

    private static int Rank(InputRowState state) => state switch
    {
        InputRowState.NeedsAttention => 4,
        InputRowState.Problem => 3,
        InputRowState.NotSetUp => 2,
        InputRowState.NeverRun => 1,
        _ => 0,
    };
}
