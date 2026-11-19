namespace NoMorePaper.Application.Inputs;

/// <summary>The background inputs (ADR-0024); the browser's upload and camera are no background input.</summary>
public enum InputKind
{
    ScannerFolder,
    Mail,
    ICloud,
}
