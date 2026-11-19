namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>What the local LLM said about a mail (<see cref="IMailAssessor"/>).</summary>
/// <param name="IsSpam">Advertising, phishing or fraud; always <c>false</c> for a known sender.</param>
/// <param name="IsDocument">The mail itself is a document worth keeping (invoice, notice, contract), not a newsletter or chat.</param>
/// <param name="Note">One or two sentences of facts from the text (deadline, how to pay, customer number), or <c>null</c>.</param>
public sealed record MailAssessment(bool IsSpam, bool IsDocument, string? Note);
