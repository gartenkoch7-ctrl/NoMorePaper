namespace NoMorePaper.Application.Inputs;

/// <summary>
/// Announces that an <see cref="IInputSource"/> of <see cref="Kind"/> is registered (keyed by its kind). The
/// scheduler learns the inputs from these, without building any source, so a source that fails to build
/// fails only its own runs.
/// </summary>
public sealed record InputSourceRegistration(InputKind Kind);
