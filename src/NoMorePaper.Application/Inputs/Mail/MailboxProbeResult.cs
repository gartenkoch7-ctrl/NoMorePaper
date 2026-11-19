namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>What <see cref="IMailboxProbe"/> found.</summary>
public enum MailboxProbeResult
{
    /// <summary>Connected and signed in.</summary>
    Ok,

    /// <summary>The server refused user name or password.</summary>
    SignInFailed,

    /// <summary>No connection: wrong host or port, no TLS, or the server does not answer in time.</summary>
    Unreachable,
}
