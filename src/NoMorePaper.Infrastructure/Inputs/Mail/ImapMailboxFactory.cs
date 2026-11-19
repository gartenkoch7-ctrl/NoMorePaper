using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Inputs.Mail;
using NoMorePaper.Application.Security;
using MailFolder = NoMorePaper.Application.Inputs.Mail.MailFolder;

namespace NoMorePaper.Infrastructure.Inputs.Mail;

/// <summary>
/// Connects and signs in to the household's IMAP mailbox and makes sure its folders exist ("Verarbeitet",
/// "NoMorePaper-Spam", "NoMorePaper-Fehler", created next to the inbox and subscribed, so mail apps show them).
/// </summary>
public sealed class ImapMailboxFactory(ISecretProtector secrets, TimeProvider clock, ILoggerFactory loggers)
{
    /// <summary>Time limit of each IMAP command, including connecting and signing in.</summary>
    public static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(60);

    private readonly ILogger _logger = loggers.CreateLogger<ImapMailbox>();

    /// <summary>The folders a mail ends in, by their names in the mailbox.</summary>
    public static IReadOnlyDictionary<MailFolder, string> FolderNames { get; } = new Dictionary<MailFolder, string>
    {
        [MailFolder.Processed] = "Verarbeitet",
        [MailFolder.Spam] = "NoMorePaper-Spam",
        [MailFolder.Failed] = "NoMorePaper-Fehler",
    };

    /// <summary>Connects and signs in within <paramref name="timeout"/> per command; the caller owns the client.</summary>
    /// <exception cref="AuthenticationException">The server refused user name or password.</exception>
    public async Task<ImapClient> ConnectAsync(MailboxSettings settings, string password, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var client = new ImapClient { Timeout = (int)timeout.TotalMilliseconds };
        try
        {
            var options = settings.Security switch
            {
                MailboxSecurity.Ssl => SecureSocketOptions.SslOnConnect,
                MailboxSecurity.StartTls => SecureSocketOptions.StartTls,
                _ => SecureSocketOptions.None,
            };
            var started = clock.GetTimestamp();
            await client.ConnectAsync(settings.Host, settings.Port, options, cancellationToken).ConfigureAwait(false);
            await client.AuthenticateAsync(settings.UserName, password, cancellationToken).ConfigureAwait(false);
            var elapsedMs = (long)clock.GetElapsedTime(started).TotalMilliseconds;
            _logger.SignedIn(elapsedMs);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>Opens the mailbox for one run.</summary>
    /// <param name="secret">The password, encrypted with <see cref="SecretPurposes.MailPassword"/>.</param>
    /// <exception cref="SecretUnreadableException">The stored password cannot be decrypted.</exception>
    /// <exception cref="AuthenticationException">The server refused user name or password.</exception>
    public async Task<ImapMailbox> OpenAsync(MailboxSettings settings, byte[] secret, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(secret);
        var password = secrets.Unprotect(SecretPurposes.MailPassword, secret);
        var client = await ConnectAsync(settings, password, CommandTimeout, cancellationToken).ConfigureAwait(false);
        try
        {
            var targets = await FoldersAsync(client, cancellationToken).ConfigureAwait(false);
            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
            return new ImapMailbox(client, inbox, targets, settings.TrustedAuthServId, clock, _logger);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private async Task<Dictionary<MailFolder, IMailFolder>> FoldersAsync(ImapClient client, CancellationToken cancellationToken)
    {
        // Next to the inbox in the personal namespace, also on servers that put everything below "INBOX.".
        var root = client.PersonalNamespaces.Count > 0
            ? client.GetFolder(client.PersonalNamespaces[0])
            : client.Inbox.ParentFolder ?? throw new InvalidOperationException("The mail server names no place for folders.");
        var subfolders = await root.GetSubfoldersAsync(false, cancellationToken).ConfigureAwait(false);
        var folders = new Dictionary<MailFolder, IMailFolder>();
        foreach (var (target, name) in FolderNames)
        {
            var folder = subfolders.FirstOrDefault(found => string.Equals(found.Name, name, StringComparison.Ordinal));
            if (folder is null)
            {
                folder = await root.CreateAsync(name, true, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("The mail server did not create a folder.");
                await folder.SubscribeAsync(cancellationToken).ConfigureAwait(false);
                _logger.FolderCreated(target);
            }

            folders[target] = folder;
        }

        return folders;
    }
}
