using System.Net.Sockets;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Application.Inputs.Mail;
using NoMorePaper.Application.Security;

namespace NoMorePaper.Infrastructure.Inputs.Mail;

/// <summary>
/// The mail input (ADR-0024): every 5 minutes, the household's mailbox as the administrator set it up
/// (<see cref="IInputConnections"/>, kind <see cref="InputKind.Mail"/>, no account) through <see cref="ProcessMailbox"/>.
/// Problems of the connection become the run's <see cref="InputProblem"/>, never exceptions.
/// </summary>
public sealed class MailSource(
    IInputConnections connections,
    ImapMailboxFactory mailboxes,
    ProcessMailbox process,
    ILogger<MailSource> logger) : IInputSource
{
    public InputKind Kind => InputKind.Mail;

    public TimeSpan Interval { get; } = TimeSpan.FromMinutes(5);

    public async Task<InputRunResult> RunAsync(CancellationToken cancellationToken)
    {
        var connection = await connections.GetAsync(InputKind.Mail, null, cancellationToken).ConfigureAwait(false);
        if (connection?.Secret is not { } secret || MailboxSettings.Parse(connection.Settings) is not { } settings)
        {
            logger.NotConfigured();
            return InputRunResult.Failed(new InputProblem(InputProblemKind.NotConfigured));
        }

        try
        {
            var mailbox = await mailboxes.OpenAsync(settings, secret, cancellationToken).ConfigureAwait(false);
            await using (mailbox.ConfigureAwait(false))
            {
                return await process.RunAsync(mailbox, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is AuthenticationException or SecretUnreadableException)
        {
            // Waiting does not help: the password has to be entered again.
            logger.SignInRefused();
            return InputRunResult.Failed(new InputProblem(InputProblemKind.SignInFailed, NeedsAttention: true));
        }
        catch (Exception exception) when (IsConnectionProblem(exception, cancellationToken))
        {
            logger.Unreachable(exception.GetType().Name);
            return InputRunResult.Failed(new InputProblem(InputProblemKind.Unreachable));
        }
        catch (Exception exception) when (exception is ImapCommandException or FolderNotFoundException)
        {
            logger.CommandRefused(exception.GetType().Name);
            return InputRunResult.Failed(new InputProblem(InputProblemKind.Failed));
        }
    }

    // The worker stopping is no problem of the mailbox; a time limit of MailKit is.
    private static bool IsConnectionProblem(Exception exception, CancellationToken cancellationToken) =>
        exception is SocketException or IOException or TimeoutException or SslHandshakeException
            or ImapProtocolException or ServiceNotConnectedException or ServiceNotAuthenticatedException
        || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);
}
