namespace NoMorePaper.Application.Inputs;

/// <summary>A problem of an input run.</summary>
/// <param name="AccountId">The account whose part of the input has the problem (e.g. its iCloud), or <c>null</c> for the whole input.</param>
/// <param name="NeedsAttention">Somebody has to act (enter a code, fix the password); it does not go away by waiting.</param>
public sealed record InputProblem(InputProblemKind Kind, Guid? AccountId = null, bool NeedsAttention = false);
