using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using MimeKit;
using NoMorePaper.Application.Inputs.Mail;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Inputs.Mail;

/// <summary>
/// A throwaway IMAP server (GreenMail, Apache-2.0) with mailboxes <c>box1</c>…<c>box12</c>, so every test
/// has an inbox of its own. Mails are put in with APPEND, as if the own mail server had delivered them.
/// </summary>
public sealed class GreenMailFixture : IAsyncLifetime
{
    /// <summary>Pinned by digest (2.1.15, 2026-10-25).</summary>
    public const string Image = "greenmail/standalone:2.1.15@sha256:309ba544ea482e5d4265d09921cdab07c8dad2cc4481d715c9b8ff5d3f0a149f";

    public const string Password = "geheim-123";

    public const string Domain = "haushalt.example";

    /// <summary>The own mail server's id in <c>Authentication-Results</c> in these tests.</summary>
    public const string AuthServId = "mx.haushalt.example";

    private const int ImapPort = 3143;

    private readonly IContainer _container = new ContainerBuilder(Image)
        .WithEnvironment(
            "GREENMAIL_OPTS",
            "-Dgreenmail.setup.test.imap -Dgreenmail.hostname=0.0.0.0 -Dgreenmail.verbose=false -Dgreenmail.users="
                + string.Join(',', Enumerable.Range(1, 12).Select(i => $"box{i}:{Password}@{Domain}")))
        .WithPortBinding(ImapPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(ImapPort))
        .Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);

    public MailboxSettings Settings(string box) =>
        new(_container.Hostname, _container.GetMappedPublicPort(ImapPort), MailboxSecurity.None, box, AuthServId);

    public async Task AppendAsync(string box, MimeMessage message)
    {
        using var client = await ConnectAsync(box);
        await client.Inbox.AppendAsync(message, MessageFlags.None);
        await client.DisconnectAsync(true);
    }

    /// <summary>The subjects in <paramref name="folder"/> (<c>INBOX</c> or a folder next to it); empty when it does not exist.</summary>
    public async Task<List<string>> SubjectsAsync(string box, string folder)
    {
        using var client = await ConnectAsync(box);
        var mailFolder = folder == "INBOX"
            ? client.Inbox
            : (await client.GetFolder(client.PersonalNamespaces[0]).GetSubfoldersAsync()).FirstOrDefault(found => found.Name == folder);
        if (mailFolder is null)
        {
            return [];
        }

        await mailFolder.OpenAsync(FolderAccess.ReadOnly);
        var uids = await mailFolder.SearchAsync(MailKit.Search.SearchQuery.NotDeleted);
        var subjects = new List<string>();
        foreach (var uid in uids)
        {
            subjects.Add((await mailFolder.GetMessageAsync(uid)).Subject ?? string.Empty);
        }

        await client.DisconnectAsync(true);
        return subjects;
    }

    /// <summary>Moves every mail of <paramref name="folder"/> back into the inbox, as a person would in a mail app.</summary>
    public async Task MoveBackAsync(string box, string folder)
    {
        using var client = await ConnectAsync(box);
        var source = (await client.GetFolder(client.PersonalNamespaces[0]).GetSubfoldersAsync()).First(found => found.Name == folder);
        await source.OpenAsync(FolderAccess.ReadWrite);
        await source.MoveToAsync(await source.SearchAsync(MailKit.Search.SearchQuery.All), client.Inbox);
        await client.DisconnectAsync(true);
    }

    /// <summary>Deletes a folder next to the inbox, so that moving there fails until it is created again.</summary>
    public async Task DeleteFolderAsync(string box, string folder)
    {
        using var client = await ConnectAsync(box);
        var target = (await client.GetFolder(client.PersonalNamespaces[0]).GetSubfoldersAsync()).First(found => found.Name == folder);
        await target.DeleteAsync();
        await client.DisconnectAsync(true);
    }

    private async Task<ImapClient> ConnectAsync(string box)
    {
        var settings = Settings(box);
        var client = new ImapClient();
        await client.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.None);
        await client.AuthenticateAsync(settings.UserName, Password);
        return client;
    }
}
