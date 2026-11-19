using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Application.Inputs.Mail;
using NoMorePaper.Application.Security;
using NoMorePaper.Application.Tests.Accounts;
using Xunit;

namespace NoMorePaper.Application.Tests.Inputs.Mail;

/// <summary>Setting up the household's mailbox: administrator only, password encrypted, kept when left empty.</summary>
public sealed class ConfigureMailboxTests
{
    private static readonly MailboxSettings s_settings = new("imap.haushalt.example", 993, MailboxSecurity.Ssl, "dokumente", "mx.haushalt.example");

    private readonly Connections _connections = new();
    private readonly Probe _probe = new();
    private readonly FakeCurrentAccount _current = new() { IsAdmin = true };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Save_StoresTheSettings_AndOnlyTheEncryptedPassword()
    {
        Assert.Equal(MailboxSetupStatus.Done, await Configure().SaveAsync(s_settings, "geheim", Ct));

        var stored = _connections.Stored!;
        Assert.Equal((InputKind.Mail, (Guid?)null), (stored.Kind, stored.AccountId));
        Assert.Equal(s_settings, MailboxSettings.Parse(stored.Settings));
        Assert.Equal("MailPassword:geheim", Encoding.UTF8.GetString(stored.Secret!));
        Assert.Equal(new MailboxSetup(s_settings, true), await Configure().GetAsync(Ct));
    }

    [Fact]
    public async Task AnEmptyPassword_KeepsTheStoredOne_AndWithoutOneItIsMissing()
    {
        Assert.Equal(MailboxSetupStatus.PasswordMissing, await Configure().SaveAsync(s_settings, null, Ct));
        Assert.Null(_connections.Stored);

        await Configure().SaveAsync(s_settings, "geheim", Ct);
        Assert.Equal(MailboxSetupStatus.Done, await Configure().SaveAsync(s_settings with { Port = 143, Security = MailboxSecurity.StartTls }, string.Empty, Ct));

        Assert.Equal("MailPassword:geheim", Encoding.UTF8.GetString(_connections.Stored!.Secret!));
        Assert.Equal(143, MailboxSettings.Parse(_connections.Stored.Settings)!.Port);
    }

    [Fact]
    public async Task Test_UsesTheGivenPassword_OrTheStoredOne()
    {
        _probe.Result = MailboxProbeResult.SignInFailed;
        Assert.Equal((MailboxSetupStatus.Done, MailboxProbeResult.SignInFailed), await Configure().TestAsync(s_settings, "neu", Ct));
        Assert.Equal("neu", _probe.Password);

        await Configure().SaveAsync(s_settings, "geheim", Ct);
        _probe.Result = MailboxProbeResult.Ok;
        Assert.Equal((MailboxSetupStatus.Done, MailboxProbeResult.Ok), await Configure().TestAsync(s_settings with { Host = "other.example" }, null, Ct));
        Assert.Equal(("geheim", "other.example"), (_probe.Password, _probe.Settings?.Host));
    }

    [Fact]
    public async Task OnlyTheAdministrator_SeesOrChangesTheMailbox()
    {
        _current.IsAdmin = false;

        Assert.Null(await Configure().GetAsync(Ct));
        Assert.Equal(MailboxSetupStatus.Forbidden, await Configure().SaveAsync(s_settings, "geheim", Ct));
        Assert.Equal((MailboxSetupStatus.Forbidden, (MailboxProbeResult?)null), await Configure().TestAsync(s_settings, "geheim", Ct));
        Assert.Null(_connections.Stored);
        Assert.Null(_probe.Password);
    }

    [Fact]
    public async Task UnusableSettings_AreRefused()
    {
        Assert.Equal(MailboxSetupStatus.Invalid, await Configure().SaveAsync(s_settings with { Port = 0 }, "geheim", Ct));
        Assert.Equal(MailboxSetupStatus.Invalid, (await Configure().TestAsync(s_settings with { Host = "a b" }, "geheim", Ct)).Status);
        Assert.Null(_connections.Stored);
    }

    private ConfigureMailbox Configure() =>
        new(_connections, new Secrets(), _probe, _current, TimeProvider.System, NullLogger<ConfigureMailbox>.Instance);

    private sealed class Connections : IInputConnections
    {
        public InputConnection? Stored { get; private set; }

        public Task<InputConnection?> GetAsync(InputKind kind, Guid? accountId, CancellationToken cancellationToken) => Task.FromResult(Stored);

        public Task SaveAsync(InputConnection connection, CancellationToken cancellationToken)
        {
            Stored = connection;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(InputKind kind, Guid? accountId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<InputConnection>> ListAsync(InputKind kind, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    // Marks the purpose instead of encrypting, so the test sees what was protected for which purpose.
    private sealed class Secrets : ISecretProtector
    {
        public byte[] Protect(string purpose, string secret) => Encoding.UTF8.GetBytes(purpose + ":" + secret);

        public string Unprotect(string purpose, byte[] data) => Encoding.UTF8.GetString(data)[(purpose.Length + 1)..];
    }

    private sealed class Probe : IMailboxProbe
    {
        public MailboxProbeResult Result { get; set; }

        public string? Password { get; private set; }

        public MailboxSettings? Settings { get; private set; }

        public Task<MailboxProbeResult> ProbeAsync(MailboxSettings settings, string password, CancellationToken cancellationToken)
        {
            (Settings, Password) = (settings, password);
            return Task.FromResult(Result);
        }
    }
}
