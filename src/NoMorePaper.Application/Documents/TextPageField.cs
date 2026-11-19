namespace NoMorePaper.Application.Documents;

/// <summary>One header line of a <see cref="TextPage"/>: "Label: value".</summary>
public sealed record TextPageField(string Label, string Value);
