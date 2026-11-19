namespace NoMorePaper.Domain.Documents;

/// <summary>
/// "From the mail": where a document that came by mail comes from, and what the mail itself said that
/// matters (a deadline, how to pay). Shown next to the fields; never written to logs.
/// </summary>
public sealed record MailOrigin
{
    /// <summary>Longest address allowed by RFC 5321 (64 + 1 + 255).</summary>
    public const int MaxSenderAddressLength = 320;

    /// <summary>Longer subjects are cut: they are shown, not searched.</summary>
    public const int MaxSubjectLength = 300;

    /// <summary>Longer notes are cut: one or two sentences are meant.</summary>
    public const int MaxNoteLength = 500;

    /// <param name="senderAddress">Address the mail came from, as the mail said.</param>
    /// <param name="subject">Subject of the mail; may be empty. Cut to <see cref="MaxSubjectLength"/>.</param>
    /// <param name="note">What matters from the mail text, or <c>null</c>/blank for nothing useful. Cut to <see cref="MaxNoteLength"/>.</param>
    /// <exception cref="ArgumentException">The address is blank or longer than <see cref="MaxSenderAddressLength"/>.</exception>
    public MailOrigin(string senderAddress, string subject, string? note)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var address = Clean(senderAddress ?? string.Empty);
        if (address.Length == 0 || address.Length > MaxSenderAddressLength)
        {
            throw new ArgumentException($"Expected an address of 1 to {MaxSenderAddressLength} characters.", nameof(senderAddress));
        }

        SenderAddress = address;
        Subject = Cut(Clean(subject), MaxSubjectLength);
        var cleanNote = note is null ? string.Empty : Clean(note);
        Note = cleanNote.Length == 0 ? null : Cut(cleanNote, MaxNoteLength);
    }

    public string SenderAddress { get; }

    public string Subject { get; }

    /// <summary>One or two sentences from the mail text; <c>null</c> when it said nothing useful.</summary>
    public string? Note { get; }

    // Mail headers may carry control characters (folded lines, NUL), which Postgres refuses.
    private static string Clean(string value) =>
        string.Concat(value.Select(c => char.IsControl(c) ? ' ' : c)).Trim();

    private static string Cut(string value, int max) => value.Length > max ? value[..max].TrimEnd() : value;
}
