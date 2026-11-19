namespace NoMorePaper.Web.Client.Classification;

/// <summary>How the owner answered the check of a document of unknown origin.</summary>
/// <param name="HandedOver"><c>true</c>: the document went to another account; <c>false</c>: the owner keeps it.</param>
/// <param name="ReceiverName">Display name of the account that got it, when <paramref name="HandedOver"/>.</param>
public sealed record OwnerCheckAnswer(bool HandedOver, string? ReceiverName = null);
