using NoMorePaper.Application.Documents;

namespace NoMorePaper.Application.Tests.Inputs.Mail;

/// <summary>Writes a tiny PDF head for each text page and keeps the pages.</summary>
internal sealed class FakePdfComposer : IPdfComposer
{
    public List<TextPage> Pages { get; } = [];

    public Exception? Failure { get; set; }

    public Task ComposeImagesAsync(IReadOnlyList<Stream> jpegs, Stream target, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public async Task ComposeTextAsync(TextPage page, Stream target, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Pages.Add(page);
        await target.WriteAsync("%PDF-1.7\n"u8.ToArray(), cancellationToken);
    }
}
