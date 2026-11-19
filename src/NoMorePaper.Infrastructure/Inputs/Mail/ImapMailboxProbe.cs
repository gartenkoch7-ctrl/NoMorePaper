using System.Net.Sockets;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Inputs.Mail;

namespace NoMorePaper.Infrastructure.Inputs.Mail;

/// <summary><see cref="IMailboxProbe"/> over IMAP: connect and sign in with <see cref="ImapMailboxFactory"/>, sign off, within 20 s.</summary>
public sealed class ImapMailboxProbe(ImapMailboxFactory mailboxes, TimeProvider clock, ILogger<ImapMailboxProbe> logger) : IMailboxProbe
{
    /// <summary>The administrator waits for the answer on the settings page.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    public async Task<MailboxProbeResult> ProbeAsync(MailboxSettings settings, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        using var limit = new CancellationTokenSource(Timeout, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, limit.Token);
        try
        {
            using var client = await mailboxes.ConnectAsync(settings, password, Timeout, linked.Token).ConfigureAwait(false);
            await client.DisconnectAsync(true, linked.Token).ConfigureAwait(false);
            return MailboxProbeResult.Ok;
        }
        catch (AuthenticationException)
        {
            logger.SignInRefused();
            return MailboxProbeResult.SignInFailed;
        }
        catch (Exception exception) when (exception is SocketException or IOException or TimeoutException or SslHandshakeException
            or ImapProtocolException or ImapCommandException or ServiceNotConnectedException or NotSupportedException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.Unreachable(exception.GetType().Name);
            return MailboxProbeResult.Unreachable;
        }
    }
}
