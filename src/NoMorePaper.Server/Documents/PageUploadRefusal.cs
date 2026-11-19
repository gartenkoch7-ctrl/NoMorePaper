namespace NoMorePaper.Server.Documents;

/// <summary>Why camera pages are refused: a fixed reason word for logs, the HTTP status and the ProblemDetails title.</summary>
internal sealed record PageUploadRefusal(string Reason, int Status, string Title);
