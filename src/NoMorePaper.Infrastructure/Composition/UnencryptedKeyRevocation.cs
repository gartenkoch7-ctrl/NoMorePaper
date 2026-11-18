using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NoMorePaper.Infrastructure.Composition;

/// <summary>
/// Keys written before the key ring certificate existed (before M2) lie in the key folder in clear
/// text, and one of them stays the default key for up to 90 days. Once a certificate is configured,
/// every such key that is not revoked yet is revoked, so the framework creates a new, encrypted
/// default key. Only cookies are protected with these keys: everybody is signed out once.
/// </summary>
internal sealed partial class UnencryptedKeyRevocation(
    IKeyManager keys, IOptions<KeyManagementOptions> options, ILogger<UnencryptedKeyRevocation> logger)
{
    public const string Reason = "unencrypted key from before M2";

    /// <returns>How many keys were revoked.</returns>
    public int RevokeUnencryptedKeys()
    {
        // Without a configured folder the framework's default location applies (development only).
        if (options.Value.XmlRepository is not { } repository)
        {
            return 0;
        }

        var active = keys.GetAllKeys().Where(key => !key.IsRevoked).Select(key => key.KeyId).ToHashSet();
        var unencrypted = repository.GetAllElements()
            .Where(element => element.Name.LocalName == "key" && !IsEncrypted(element))
            .Select(element => Guid.TryParse((string?)element.Attribute("id"), out var id) ? id : Guid.Empty)
            .Where(active.Contains)
            .ToList();

        // One key at a time, not all keys: an encrypted key that Server or Worker just created stays valid.
        foreach (var id in unencrypted)
        {
            keys.RevokeKey(id, Reason);
        }

        if (unencrypted.Count > 0)
        {
            KeysRevoked(logger, unencrypted.Count);
        }

        return unencrypted.Count;
    }

    private static bool IsEncrypted(XElement key) => key.Descendants().Any(element => element.Name.LocalName == "encryptedSecret");

    [LoggerMessage(EventId = 3410, Level = LogLevel.Warning, Message = "Revoked {KeyCount} unencrypted data protection keys from before the key ring certificate; every session is signed out once")]
    private static partial void KeysRevoked(ILogger logger, int keyCount);
}
