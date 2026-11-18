namespace NoMorePaper.Architecture.Tests;

/// <summary>One production source file: full path, owning project (folder under src/) and text.</summary>
internal sealed record SourceFile(string Path, string Project, string Text);
