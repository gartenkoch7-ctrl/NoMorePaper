namespace NoMorePaper.Server.Tests.Hosting;

/// <summary>The parts of a ProblemDetails answer the tests look at; <see cref="Errors"/> only for validation problems.</summary>
internal sealed record ProblemBody(int Status, string? Title, Dictionary<string, string[]>? Errors);
