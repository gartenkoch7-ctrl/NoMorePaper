using System.Security.Cryptography;
using System.Text;
using ICloudSpike.Http;
using ICloudSpike.Session;
using ICloudSpike.Terminal;

namespace ICloudSpike.Auth;

/// <summary>
/// Gets a usable iCloud session with as little user input as possible: stored cookies first, then
/// the stored session token, and only then password and verification code.
/// </summary>
internal sealed class LoginCoordinator
{
    private readonly IUserConsole _console;
    private readonly AppleAuthClient _auth;
    private readonly VerificationCodeClient _codes;
    private readonly SetupClient _setup;
    private readonly SessionData _session;
    private readonly TimeProvider _clock;
    private readonly Action _saveSession;

    public LoginCoordinator(
        IUserConsole console,
        AppleAuthClient auth,
        VerificationCodeClient codes,
        SetupClient setup,
        SessionData session,
        TimeProvider clock,
        Action saveSession)
    {
        _console = console;
        _auth = auth;
        _codes = codes;
        _setup = setup;
        _session = session;
        _clock = clock;
        _saveSession = saveSession;
    }

    /// <param name="preflight">Load the sign-in frame before the password step, like a browser.</param>
    /// <param name="cancellationToken">Ends the dialogue.</param>
    public async Task<LoginResult> EnsureLoggedInAsync(bool preflight, CancellationToken cancellationToken)
    {
        if (_session.Cookies.Count > 0)
        {
            var validated = await _setup.ValidateAsync(cancellationToken);
            if (validated is { RequiresTwoFactor: false })
            {
                return new LoginResult(validated, LoginPath.ReusedSession);
            }
        }

        if (_session.SessionToken is not null)
        {
            try
            {
                var renewed = await _setup.AccountLoginAsync(cancellationToken);
                if (!renewed.RequiresTwoFactor)
                {
                    _saveSession();
                    return new LoginResult(renewed, LoginPath.RenewedByToken);
                }
            }
            catch (ICloudException exception) when (exception.Failure == ICloudFailure.SessionExpired)
            {
                // Expected once the token is too old; the password sign-in below replaces it.
            }
        }

        return await SignInWithPasswordAsync(preflight, cancellationToken);
    }

    private async Task<LoginResult> SignInWithPasswordAsync(bool preflight, CancellationToken cancellationToken)
    {
        _console.WriteLine("Keine gültige Sitzung gespeichert – Anmeldung mit Apple-ID und Passwort.");

        // Apple treats the Apple-ID as case-insensitive, but the name is part of the SRP proof,
        // so it has to be sent in one canonical spelling.
        var accountName = _console.ReadLine("Apple-ID (E-Mail-Adresse): ").Trim().ToLowerInvariant();
        if (accountName.Length == 0)
        {
            throw new ICloudException(ICloudFailure.Cancelled, "Sign-in: no Apple-ID entered.");
        }

        var passwordChars = _console.ReadSecret("Passwort (wird nicht angezeigt): ");
        var passwordUtf8 = Encoding.UTF8.GetBytes(passwordChars);
        Array.Clear(passwordChars);
        SignInOutcome outcome;
        try
        {
            if (passwordUtf8.Length == 0)
            {
                throw new ICloudException(ICloudFailure.Cancelled, "Sign-in: no password entered.");
            }

            if (preflight)
            {
                await _auth.PreflightAsync(cancellationToken);
            }

            outcome = await _auth.SignInAsync(accountName, passwordUtf8, cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordUtf8);
        }

        var codeUsed = false;
        if (outcome == SignInOutcome.TwoFactorRequired)
        {
            await ConfirmCodeAsync(cancellationToken);
            codeUsed = true;
        }

        var account = await _setup.AccountLoginAsync(cancellationToken);
        if (account.RequiresTwoFactor && !codeUsed)
        {
            // Apple accepted the password with 200 but iCloud still considers the session untrusted.
            await ConfirmCodeAsync(cancellationToken);
            codeUsed = true;
            account = await _setup.AccountLoginAsync(cancellationToken);
        }

        if (account.RequiresTwoFactor)
        {
            throw new ICloudException(
                ICloudFailure.TwoFactorFailed, "Sign-in: iCloud still reports the session as untrusted.");
        }

        _saveSession();
        return new LoginResult(account, codeUsed ? LoginPath.PasswordAndCode : LoginPath.PasswordOnly);
    }

    private async Task ConfirmCodeAsync(CancellationToken cancellationToken)
    {
        await new TwoFactorDialogue(_console, _codes).RunAsync(cancellationToken);
        _session.TrustedAt = _clock.GetUtcNow();

        // Saved right away: a later failure must not cost the user another verification code.
        _saveSession();
    }
}
