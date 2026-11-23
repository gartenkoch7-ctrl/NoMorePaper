using MudBlazor;

namespace NoMorePaper.Web.Client.Design;

/// <summary>The symbol of each status, shared by every place that shows a status. Pure logic.</summary>
public static class StatusIcons
{
    public static string For(StatusKind kind) => kind switch
    {
        StatusKind.Waiting => Icons.Material.Filled.Schedule,
        StatusKind.Processing => Icons.Material.Filled.Sync,
        StatusKind.Done => Icons.Material.Filled.CheckCircle,
        StatusKind.Failed => Icons.Material.Filled.Error,
        _ => Icons.Material.Filled.AutoAwesome,
    };
}
