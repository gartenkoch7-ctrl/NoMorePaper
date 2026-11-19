using NoMorePaper.Application.Inputs.Mail;

namespace NoMorePaper.Application.Tests.Inputs.Mail;

/// <summary>An inbox in memory: mails by handle, and where each was moved.</summary>
internal sealed class FakeMailbox : IMailbox
{
    private uint _nextUid = 1;

    public List<(MailHandle Handle, MailContent? Content)> Inbox { get; } = [];

    public List<(MailHandle Handle, MailFolder Folder)> Moved { get; } = [];

    /// <summary>Moves fail while set.</summary>
    public Exception? MoveFailure { get; set; }

    /// <summary>Read fails with this for every mail while set (e.g. the connection dropped).</summary>
    public Exception? ReadFailure { get; set; }

    public int Reads { get; private set; }

    /// <param name="content"><c>null</c> for a mail that cannot be read as MIME.</param>
    public MailHandle Add(MailContent? content, string? key = null)
    {
        var handle = new MailHandle(_nextUid++, key ?? "id:" + Guid.NewGuid());
        Inbox.Add((handle, content));
        return handle;
    }

    public Task<IReadOnlyList<MailHandle>> NewMailAsync(int max, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MailHandle>>([.. Inbox.Select(entry => entry.Handle).Take(max)]);

    public Task<MailContent?> ReadAsync(MailHandle mail, CancellationToken cancellationToken)
    {
        Reads++;
        if (ReadFailure is not null)
        {
            throw ReadFailure;
        }

        var entry = Inbox.Find(found => found.Handle == mail);
        if (entry.Handle is null)
        {
            return Task.FromResult<MailContent?>(null);
        }

        return entry.Content is null ? throw new MailUnreadableException() : Task.FromResult<MailContent?>(entry.Content);
    }

    public Task MoveAsync(MailHandle mail, MailFolder folder, CancellationToken cancellationToken)
    {
        if (MoveFailure is not null)
        {
            throw MoveFailure;
        }

        Inbox.RemoveAll(entry => entry.Handle == mail);
        Moved.Add((mail, folder));
        return Task.CompletedTask;
    }
}
