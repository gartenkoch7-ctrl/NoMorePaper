namespace NoMorePaper.Web.Client.Camera;

/// <summary>
/// Decides when a camera hint is spoken: at most every <see cref="MinInterval"/>, and the same hint
/// again only after <see cref="RepeatInterval"/>. The camera reports a hint when it changes
/// (<see cref="Show"/>); the page asks several times a second whether one is due (<see cref="Due"/>), so a
/// hint held back by the pause is still spoken once the pause is over. A service with
/// <see cref="TimeProvider"/> (AGENTS.md 2.6), one per page visit.
/// </summary>
public sealed class CameraAnnouncer(TimeProvider time)
{
    /// <summary>Shortest pause between two announcements; a screen reader needs time to finish speaking.</summary>
    public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(2.5);

    /// <summary>A hint that still applies is repeated after this long, so it is not forgotten.</summary>
    public static readonly TimeSpan RepeatInterval = TimeSpan.FromSeconds(6);

    // Hint words of wwwroot/js/camera/guidance.js → text keys. "ready" has none: the picture is taken at once.
    private static readonly Dictionary<string, string> s_keys = new(StringComparer.Ordinal)
    {
        ["search"] = "CamHintSearch",
        ["farther"] = "CamHintFarther",
        ["closer"] = "CamHintCloser",
        ["left"] = "CamHintLeft",
        ["right"] = "CamHintRight",
        ["up"] = "CamHintUp",
        ["down"] = "CamHintDown",
        ["hold"] = "CamHintHold",
    };

    private string? _current;
    private string? _lastSpoken;
    private DateTimeOffset? _lastAt;

    /// <summary>The text key of a hint word, or <c>null</c> for a word without text (<c>ready</c>) or an unknown one.</summary>
    public static string? KeyFor(string? hint) => hint is not null && s_keys.TryGetValue(hint, out var key) ? key : null;

    /// <summary>The hint that applies now (from the camera); <c>null</c> when none applies.</summary>
    public void Show(string? hint) => _current = hint;

    /// <returns>The text key to announce now, or <c>null</c> if nothing is due.</returns>
    public string? Due()
    {
        if (KeyFor(_current) is not { } key)
        {
            return null;
        }

        var now = time.GetUtcNow();
        if (_lastAt is { } last && (now - last < MinInterval || (_current == _lastSpoken && now - last < RepeatInterval)))
        {
            return null;
        }

        _lastAt = now;
        _lastSpoken = _current;
        return key;
    }

    /// <summary>
    /// Something else was just announced (camera ready, page taken): the next hint waits for the pause,
    /// and may then be the one spoken before.
    /// </summary>
    public void Announced()
    {
        _lastAt = time.GetUtcNow();
        _lastSpoken = null;
    }
}
