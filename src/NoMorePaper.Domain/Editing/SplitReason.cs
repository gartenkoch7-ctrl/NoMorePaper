namespace NoMorePaper.Domain.Editing;

/// <summary>Why a scanned stack is probably several documents (M4b, stack separation).</summary>
public enum SplitReason
{
    /// <summary>A probably blank page separates two documents.</summary>
    Blank,

    /// <summary>A page counter starts again ("Seite 1 von 3", "Page 1 of 2").</summary>
    PageCounter,

    /// <summary>A new salutation at the top of a page ("Sehr geehrte", "Dear").</summary>
    Salutation,

    /// <summary>The local LLM saw another sender.</summary>
    SenderChange,
}
