using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.Extensions.Logging;
using MimeKit;
using NoMorePaper.Application.Inputs.Mail;
using MailFolder = NoMorePaper.Application.Inputs.Mail.MailFolder;

namespace NoMorePaper.Infrastructure.Inputs.Mail;

/// <summary>
/// <see cref="IMailbox"/> over an IMAP connection (MailKit) for one run; opened by <see cref="ImapMailboxFactory"/>.
/// The inbox is open read-write; mails are moved by UID, so nothing else in the inbox is touched.
/// </summary>
public sealed class ImapMailbox : IMailbox, IAsyncDisposable
{
    private readonly ImapClient _client;
    private readonly IMailFolder _inbox;
    private readonly IReadOnlyDictionary<MailFolder, IMailFolder> _targets;
    private readonly string? _trustedAuthServId;
    private readonly TimeProvider _clock;
    private readonly ILogger _logger;

    internal ImapMailbox(
        ImapClient client,
        IMailFolder inbox,
        IReadOnlyDictionary<MailFolder, IMailFolder> targets,
        string? trustedAuthServId,
        TimeProvider clock,
        ILogger logger)
    {
        _client = client;
        _inbox = inbox;
        _targets = targets;
        _trustedAuthServId = trustedAuthServId;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MailHandle>> NewMailAsync(int max, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max);

        // A mail marked deleted waits for an expunge by its owner's mail app; it is gone for us.
        var all = await _inbox.SearchAsync(SearchQuery.NotDeleted, cancellationToken).ConfigureAwait(false);
        var oldest = all.OrderBy(uid => uid.Id).Take(max).ToList();
        if (oldest.Count == 0)
        {
            return [];
        }

        var summaries = await _inbox.FetchAsync(oldest, MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.Size, cancellationToken)
            .ConfigureAwait(false);
        return [.. summaries
            .OrderBy(summary => summary.UniqueId.Id)
            .Select(summary => new MailHandle(
                summary.UniqueId.Id,
                MailMessageReader.ItemKey(
                    summary.Envelope?.MessageId,
                    summary.Envelope?.Date,
                    summary.Envelope?.From.Mailboxes.FirstOrDefault()?.Address,
                    summary.Envelope?.Subject,
                    summary.Size)))];
    }

    public async Task<MailContent?> ReadAsync(MailHandle mail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mail);
        MimeMessage message;
        try
        {
            message = await _inbox.GetMessageAsync(new UniqueId(mail.Uid), cancellationToken).ConfigureAwait(false);
        }
        catch (MessageNotFoundException)
        {
            return null;
        }
        catch (Exception exception) when (exception is FormatException or ParseException)
        {
            _logger.Unreadable(exception.GetType().Name);
            throw new MailUnreadableException("The mail cannot be parsed as MIME.", exception);
        }

        try
        {
            return MailMessageReader.Read(message, _trustedAuthServId, _clock.GetUtcNow());
        }
        catch (Exception exception) when (exception is FormatException or ParseException or NotSupportedException or InvalidOperationException)
        {
            // E.g. an unknown transfer encoding or a broken header the parser let through.
            _logger.Unreadable(exception.GetType().Name);
            throw new MailUnreadableException("The mail cannot be read.", exception);
        }
    }

    public async Task MoveAsync(MailHandle mail, MailFolder folder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mail);
        var uid = new UniqueId(mail.Uid);
        var target = _targets[folder];
        if (_client.Capabilities.HasFlag(ImapCapabilities.Move))
        {
            await _inbox.MoveToAsync(uid, target, cancellationToken).ConfigureAwait(false);
            return;
        }

        await _inbox.CopyToAsync(uid, target, cancellationToken).ConfigureAwait(false);
        await _inbox.StoreAsync(uid, new StoreFlagsRequest(StoreAction.Add, MessageFlags.Deleted) { Silent = true }, cancellationToken).ConfigureAwait(false);

        // Without UIDPLUS an EXPUNGE would also remove mails others marked deleted; the mark alone hides it from us.
        var expunge = _client.Capabilities.HasFlag(ImapCapabilities.UidPlus);
        if (expunge)
        {
            await _inbox.ExpungeAsync([uid], cancellationToken).ConfigureAwait(false);
        }

        _logger.MovedByCopy(expunge);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_client.IsConnected)
            {
                using var quick = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _client.DisconnectAsync(true, quick.Token).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or ImapProtocolException or ServiceNotConnectedException)
        {
            // Signing off is a courtesy; the connection is closed below either way.
        }
        finally
        {
            _client.Dispose();
        }
    }
}
