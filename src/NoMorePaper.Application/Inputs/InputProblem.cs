namespace NoMorePaper.Application.Inputs;

/// <summary>A problem of an input run, of the whole input (<see cref="InputRunResult"/>) or of one account's part (<see cref="InputAccountResult"/>).</summary>
/// <param name="NeedsAttention">Somebody has to act (enter a code, fix the password); it does not go away by waiting.</param>
public sealed record InputProblem(InputProblemKind Kind, bool NeedsAttention = false);
