namespace NoMorePaper.Application.Recognition;

/// <summary>
/// Whether the model is ready (one per Worker). Until then recognition jobs are not claimed, so they do
/// not use up their attempts while the model is still downloading on the first start.
/// </summary>
public sealed class RecognitionReadiness
{
    private volatile bool _ready;

    public bool IsReady => _ready;

    /// <summary>Called once the model is available; recognition jobs are claimed from then on.</summary>
    public void MarkReady() => _ready = true;
}
