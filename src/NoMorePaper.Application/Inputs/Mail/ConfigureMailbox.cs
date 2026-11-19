using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Security;

namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>
/// The administrator sets up the household's mailbox (ADR-0024): server, port, security, user name, the own
/// server's <c>authserv-id</c> and the password, which is stored only encrypted (<see cref="ISecretProtector"/>)
/// and never given back. An empty password keeps the stored one, but only for the same server and user
/// (<see cref="MailboxSettings.IsSameSignIn"/>); another server or user needs the password again.
/// </summary>
public sealed class ConfigureMailbox(
    IInputConnections connections,
    ISecretProtector secrets,
    IMailboxProbe probe,
    ICurrentAccount current,
    TimeProvider clock,
    ILogger<ConfigureMailbox> logger)
{
    /// <returns>The set-up without password, or <c>null</c> when the account is not the administrator.</returns>
    public async Task<MailboxSetup?> GetAsync(CancellationToken cancellationToken)
    {
        if (!current.IsAdmin)
        {
            logger.AdministratorOnly(current.Id, "get-mailbox");
            return null;
        }

        var stored = await connections.GetAsync(InputKind.Mail, null, cancellationToken).ConfigureAwait(false);
        return new MailboxSetup(stored is null ? null : MailboxSettings.Parse(stored.Settings), stored?.Secret is not null);
    }

    /// <param name="password">A new password, or <c>null</c>/empty to keep the stored one (same server and user only).</param>
    public async Task<MailboxSetupStatus> SaveAsync(MailboxSettings settings, string? password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var (status, stored) = await CheckAsync("save-mailbox", settings, password, cancellationToken).ConfigureAwait(false);
        if (status != MailboxSetupStatus.Done)
        {
            return status;
        }

        var secret = string.IsNullOrEmpty(password) ? stored!.Secret : secrets.Protect(SecretPurposes.MailPassword, password);
        await connections.SaveAsync(new InputConnection(InputKind.Mail, null, settings.ToJson(), secret, clock.GetUtcNow()), cancellationToken)
            .ConfigureAwait(false);
        return MailboxSetupStatus.Done;
    }

    /// <summary>
    /// Tries the given set-up before it is saved; without a new password with the stored one, and that only for
    /// the stored server and user.
    /// </summary>
    /// <returns>The status, and with <see cref="MailboxSetupStatus.Done"/> what the probe found.</returns>
    public async Task<(MailboxSetupStatus Status, MailboxProbeResult? Result)> TestAsync(
        MailboxSettings settings, string? password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var (status, stored) = await CheckAsync("test-mailbox", settings, password, cancellationToken).ConfigureAwait(false);
        if (status != MailboxSetupStatus.Done)
        {
            return (status, null);
        }

        string plain;
        try
        {
            plain = string.IsNullOrEmpty(password) ? secrets.Unprotect(SecretPurposes.MailPassword, stored!.Secret!) : password;
        }
        catch (SecretUnreadableException)
        {
            // The key ring changed; the password has to be entered again.
            return (MailboxSetupStatus.PasswordMissing, null);
        }

        return (MailboxSetupStatus.Done, await probe.ProbeAsync(settings, plain, cancellationToken).ConfigureAwait(false));
    }

    private async Task<(MailboxSetupStatus Status, InputConnection? Stored)> CheckAsync(
        string action, MailboxSettings settings, string? password, CancellationToken cancellationToken)
    {
        if (!current.IsAdmin)
        {
            logger.AdministratorOnly(current.Id, action);
            return (MailboxSetupStatus.Forbidden, null);
        }

        if (!settings.IsValid)
        {
            return (MailboxSetupStatus.Invalid, null);
        }

        if (settings.Security == MailboxSecurity.None)
        {
            return (MailboxSetupStatus.Unencrypted, null);
        }

        var stored = await connections.GetAsync(InputKind.Mail, null, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrEmpty(password) && !MayUseStoredPassword(stored, settings)
            ? (MailboxSetupStatus.PasswordMissing, stored)
            : (MailboxSetupStatus.Done, stored);
    }

    // The stored password goes only to the server and user it was entered for. Otherwise whoever changes the
    // host (an administrator's session, a forged request) could have it sent to a server of his choice.
    private static bool MayUseStoredPassword(InputConnection? stored, MailboxSettings settings) =>
        stored?.Secret is not null && settings.IsSameSignIn(MailboxSettings.Parse(stored.Settings));
}
