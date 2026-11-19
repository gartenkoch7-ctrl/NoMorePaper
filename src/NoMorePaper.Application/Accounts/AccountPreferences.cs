namespace NoMorePaper.Application.Accounts;

/// <summary>
/// What an account chose for its inputs (M4): whether new documents from the background inputs are
/// private, and the sender addresses whose mails belong to it. By default shared and without addresses.
/// </summary>
/// <param name="MailSenders">Normalized addresses (<see cref="NormalizeAddress"/>), distinct, at most <see cref="MaxMailSenders"/>.</param>
public sealed record AccountPreferences(bool NewDocumentsPrivate, IReadOnlyList<string> MailSenders)
{
    /// <summary>Most sender addresses per account.</summary>
    public const int MaxMailSenders = 20;

    /// <summary>Longest address allowed by RFC 5321.</summary>
    public const int MaxAddressLength = 320;

    /// <summary>New documents shared, no sender addresses.</summary>
    public static AccountPreferences Default { get; } = new(false, []);

    /// <summary>Checks and normalizes what an account entered.</summary>
    /// <param name="invalid">The first entry that is no valid address, or <c>null</c>.</param>
    /// <returns>The preferences, or <c>null</c> when an entry is invalid or there are too many.</returns>
    public static AccountPreferences? Create(bool newDocumentsPrivate, IEnumerable<string?> mailSenders, out string? invalid)
    {
        ArgumentNullException.ThrowIfNull(mailSenders);
        invalid = null;
        var addresses = new List<string>();
        foreach (var entry in mailSenders)
        {
            if (NormalizeAddress(entry) is not { } address)
            {
                invalid = entry ?? string.Empty;
                return null;
            }

            if (!addresses.Contains(address, StringComparer.Ordinal))
            {
                addresses.Add(address);
            }
        }

        return addresses.Count > MaxMailSenders ? null : new AccountPreferences(newDocumentsPrivate, addresses.AsReadOnly());
    }

    /// <summary>
    /// An address in the one form it is stored and compared in: trimmed, lower case, of the form
    /// <c>local@domain.tld</c> without spaces. Pure logic.
    /// </summary>
    /// <returns>The normalized address, or <c>null</c> if it is no valid address.</returns>
    public static string? NormalizeAddress(string? address)
    {
        var value = address?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(value) || value.Length > MaxAddressLength || value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            return null;
        }

        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1)
        {
            return null;
        }

        var domain = value[(at + 1)..];
        var dot = domain.LastIndexOf('.');
        return dot > 0 && dot < domain.Length - 1 && !domain.Contains("..", StringComparison.Ordinal) ? value : null;
    }
}
