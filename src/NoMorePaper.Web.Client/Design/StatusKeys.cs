namespace NoMorePaper.Web.Client.Design;

/// <summary>The text keys of the status words, so display and announcements use the same words. Pure logic.</summary>
public static class StatusKeys
{
    public static string For(StatusKind kind) => kind switch
    {
        StatusKind.Waiting => "StatusWaiting",
        StatusKind.Processing => "StatusProcessing",
        StatusKind.Done => "StatusDone",
        StatusKind.Failed => "StatusFailed",
        _ => "StatusRecognized",
    };
}
