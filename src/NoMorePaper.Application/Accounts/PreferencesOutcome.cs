namespace NoMorePaper.Application.Accounts;

/// <summary>How <see cref="ChangePreferences"/> ended; the Server maps each to one HTTP status.</summary>
public enum PreferencesOutcome
{
    Saved,

    /// <summary>An address is invalid, or there are more than <see cref="AccountPreferences.MaxMailSenders"/>.</summary>
    Invalid,

    /// <summary>A sender address belongs to another account already.</summary>
    SenderTaken,

    /// <summary>The signed-in account was removed; its session is over.</summary>
    AccountGone,
}
