using Microsoft.Extensions.Logging;

namespace NoMorePaper.Infrastructure.Security;

/// <summary>Log events of stored secrets (Infrastructure 3900–3949, docs/kb/telemetrie.md): the purpose only, never a secret.</summary>
internal static partial class SecretLog
{
    [LoggerMessage(EventId = 3901, Level = LogLevel.Warning, Message = "A stored secret for {Purpose} cannot be decrypted; it has to be entered again")]
    public static partial void SecretUnreadable(this ILogger logger, string purpose);
}
