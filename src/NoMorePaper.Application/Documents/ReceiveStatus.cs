namespace NoMorePaper.Application.Documents;

/// <summary>How <see cref="ReceiveDocument"/> ended.</summary>
public enum ReceiveStatus
{
    /// <summary>Stored and queued for processing.</summary>
    Received,

    /// <summary>The uploader may already see a document with exactly this file; nothing was kept.</summary>
    Duplicate,

    /// <summary>The signed-in account was removed; nothing was stored.</summary>
    AccountGone,
}
