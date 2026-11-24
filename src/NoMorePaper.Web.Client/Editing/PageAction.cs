namespace NoMorePaper.Web.Client.Editing;

/// <summary>What a button or key on a page card asks for.</summary>
public enum PageAction
{
    RotateLeft,
    RotateRight,
    MoveUp,
    MoveDown,
    ToggleDelete,
    ToggleSplit,
    Crop,
    AutoCrop,

    /// <summary>"Zuschnitt übernehmen" (or Enter on a corner handle).</summary>
    CropApply,

    /// <summary>"Abbrechen" (or Escape) while the crop frame is open.</summary>
    CropCancel,

    /// <summary>"Zuschnitt zurücknehmen": the page is used as stored again.</summary>
    ClearCrop,

    /// <summary>"Ecken von Hand setzen" after the automatic crop found no sheet.</summary>
    CropByHand,
}
