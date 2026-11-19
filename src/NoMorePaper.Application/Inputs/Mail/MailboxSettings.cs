using System.Text.Json;
using System.Text.Json.Serialization;

namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>
/// The household's mailbox as the administrator set it up, without the password (that is the encrypted
/// secret of the <see cref="InputConnection"/>). Stored as JSON in <see cref="InputConnection.Settings"/>.
/// </summary>
/// <param name="TrustedAuthServId">
/// The <c>authserv-id</c> of the own mail server in <c>Authentication-Results</c>; only its verdict on SPF/DMARC
/// counts. <c>null</c>: no sender counts as confirmed, every mail is from an unknown sender.
/// </param>
public sealed record MailboxSettings(string Host, int Port, MailboxSecurity Security, string UserName, string? TrustedAuthServId)
{
    /// <summary>Longest host name (DNS).</summary>
    public const int MaxHostLength = 253;

    /// <summary>Longest user name and authserv-id accepted.</summary>
    public const int MaxNameLength = 320;

    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Host and user name given, port 1–65535, no control characters or spaces. Pure logic.</summary>
    public bool IsValid =>
        Port is > 0 and <= 65535
        && Enum.IsDefined(Security)
        && IsToken(Host, MaxHostLength)
        && IsToken(UserName, MaxNameLength)
        && (TrustedAuthServId is null || IsToken(TrustedAuthServId, MaxNameLength));

    /// <returns>The settings, or <c>null</c> when the text is no valid set-up.</returns>
    public static MailboxSettings? Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            var settings = JsonSerializer.Deserialize<MailboxSettings>(json, s_json);
            return settings is { IsValid: true } ? settings : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, s_json);

    /// <summary>
    /// Whether <paramref name="other"/> signs in at the same server as the same user (host, port, security and
    /// user name). Only then may the stored password be used for it; the authserv-id plays no part. Pure logic.
    /// </summary>
    public bool IsSameSignIn(MailboxSettings? other) =>
        other is not null
        && string.Equals(Host, other.Host, StringComparison.OrdinalIgnoreCase)
        && Port == other.Port
        && Security == other.Security
        && string.Equals(UserName, other.UserName, StringComparison.Ordinal);

    private static bool IsToken(string? value, int max) =>
        !string.IsNullOrEmpty(value) && value.Length <= max && !value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c));
}
