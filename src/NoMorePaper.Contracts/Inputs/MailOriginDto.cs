namespace NoMorePaper.Contracts.Inputs;

/// <summary>"From the mail": where a document that came by mail comes from.</summary>
/// <param name="Note">What matters from the mail text in one or two sentences; <c>null</c> when it said nothing useful.</param>
public sealed record MailOriginDto(string Sender, string Subject, string? Note);
