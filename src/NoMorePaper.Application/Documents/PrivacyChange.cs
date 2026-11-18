namespace NoMorePaper.Application.Documents;

/// <summary>Outcome of <see cref="SetDocumentPrivacy"/>; the Server maps each to one HTTP status.</summary>
public enum PrivacyChange
{
    Changed,

    /// <summary>No such document, or it belongs to another account.</summary>
    NotFound,

    /// <summary>The signed-in account was removed; its session is over.</summary>
    AccountGone,
}
