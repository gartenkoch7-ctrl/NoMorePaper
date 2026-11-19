namespace NoMorePaper.Application.Inputs;

/// <summary>A row of <see cref="ListInputStatus"/>: the state with the display name of its account, if any.</summary>
public sealed record InputStatusEntry(InputStatus Status, string? AccountName);
