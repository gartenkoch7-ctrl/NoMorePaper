namespace NoMorePaper.Web.Client.Trash;

/// <summary>The question that is open (only one at a time); <paramref name="Id"/> is empty for <see cref="TrashQuestionKind.Empty"/>.</summary>
public readonly record struct TrashQuestion(TrashQuestionKind Kind, Guid Id);
