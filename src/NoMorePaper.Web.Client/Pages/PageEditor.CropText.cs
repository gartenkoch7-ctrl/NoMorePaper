using System.Globalization;
using NoMorePaper.Web.Client.Editing;

namespace NoMorePaper.Web.Client.Pages;

// What the crop says: the corners (on the handles and, once the keys rest, aloud), the result and the failures.
public sealed partial class PageEditor
{
    // A corner is announced once the keys rest this long: every step aloud would queue up behind each other.
    private static readonly TimeSpan CornerPause = TimeSpan.FromMilliseconds(600);

    private CancellationTokenSource? _cornerSay;

    /// <summary>"Ecke oben links: 3 % von links, 2 % von oben" for a corner of the open frame, as it is seen.</summary>
    private string CornerLabel(CropCorner corner)
    {
        if (_cropShape is not { } shape)
        {
            return string.Empty;
        }

        var point = CropGeometry.VisualPoint(shape.Quad, RotationOf(_cropPage), corner);
        var name = corner switch
        {
            CropCorner.TopLeft => L["CropCornerTopLeft"],
            CropCorner.TopRight => L["CropCornerTopRight"],
            CropCorner.BottomRight => L["CropCornerBottomRight"],
            _ => L["CropCornerBottomLeft"],
        };
        return L["CropCornerPosition", name.Value, Percent(point.X), Percent(point.Y)];
    }

    private static string Percent(int tenths) => (tenths / 10.0).ToString("0.#", CultureInfo.CurrentCulture);

    private void SayCornerLater(CropCorner corner)
    {
        CancelCornerSay();
        _cornerSay = new CancellationTokenSource();
        _ = SayCornerAsync(corner, _cornerSay.Token);
    }

    private async Task SayCornerAsync(CropCorner corner, CancellationToken token)
    {
        try
        {
            await Task.Delay(CornerPause, Clock, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await InvokeAsync(() => Cropping && !token.IsCancellationRequested ? Say(CornerLabel(corner)) : Task.CompletedTask);
    }

    private void CancelCornerSay()
    {
        _cornerSay?.Cancel();
        _cornerSay?.Dispose();
        _cornerSay = null;
    }

    private void Fail(int page, string reason)
    {
        Logger.PageCropFailed(reason);
        var why = reason switch
        {
            "opencv" => L["CamErrorOpenCv"].Value,
            "load" => L["ErrorUnreachable"].Value,
            _ => L["ErrorPrefix", reason].Value,
        };
        _status?.Clear();
        ShowCropNotice(page, L["CropFailed", why], notFound: false);
    }

    private void ShowCropNotice(int page, string text, bool notFound)
    {
        _cropNoticePage = page;
        _cropNotice = text;
        _cropNotFound = notFound;
    }

    // "Um 3 Grad gedreht, Rand oben 2 cm entfernt." – what the frame does, for the card and the announcement.
    private string Describe(CropMeasure measure)
    {
        var sides = new List<string>();
        void Side(int cm, string key)
        {
            if (cm > 0)
            {
                sides.Add(L[key, cm]);
            }
        }

        Side(measure.TopCm, "CropRemovedTop");
        Side(measure.RightCm, "CropRemovedRight");
        Side(measure.BottomCm, "CropRemovedBottom");
        Side(measure.LeftCm, "CropRemovedLeft");
        var edges = sides.Count > 0 ? string.Join(", ", sides) : L["CropAutoNoEdges"].Value;
        return measure.Degrees > 0 ? L["CropAutoResult", measure.Degrees, edges] : L["CropAutoStraight", edges];
    }
}
