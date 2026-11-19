namespace NoMorePaper.Application.Documents;

/// <summary>Content of a text PDF (<see cref="IPdfComposer.ComposeTextAsync"/>), e.g. a mail without attachment.</summary>
/// <param name="Heading">Line printed first, in bold.</param>
/// <param name="Header">Fields below the heading, e.g. sender, date and subject of a mail.</param>
/// <param name="Body">The text; line breaks are kept, long lines are wrapped.</param>
public sealed record TextPage(string Heading, IReadOnlyList<TextPageField> Header, string Body);
