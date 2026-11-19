namespace NoMorePaper.Domain.Documents;

/// <summary>Where the fields of a document (correspondent, type, date, amount, …) come from.</summary>
public enum RecognitionState
{
    /// <summary>Not recognized yet, or recognition failed.</summary>
    None,

    /// <summary>Set by the local LLM and not checked by anybody yet; listed in "to review".</summary>
    Automatic,

    /// <summary>Confirmed or edited by somebody who may edit the document; recognition no longer changes it.</summary>
    Confirmed,
}
