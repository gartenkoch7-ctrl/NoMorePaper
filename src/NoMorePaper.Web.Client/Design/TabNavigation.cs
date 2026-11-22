namespace NoMorePaper.Web.Client.Design;

/// <summary>Keyboard rules of a tab list (WAI-ARIA tabs pattern); pure so it can be tested.</summary>
public static class TabNavigation
{
    /// <summary>
    /// Index of the tab that a key moves to. Arrow keys wrap around; any other key keeps
    /// <paramref name="current"/>.
    /// </summary>
    public static int Next(int current, int count, string key)
    {
        if (count <= 0)
        {
            return current;
        }

        return key switch
        {
            "ArrowRight" => (current + 1) % count,
            "ArrowLeft" => (current - 1 + count) % count,
            "Home" => 0,
            "End" => count - 1,
            _ => current,
        };
    }
}
